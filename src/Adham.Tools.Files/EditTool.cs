using System.ComponentModel;
using System.Globalization;
using System.Text;
using Adham.Tools.Abstractions;
using Microsoft.Extensions.AI;

namespace Adham.Tools.Files;

public sealed class EditTool(WorkingDirectory cwd, FileReadTracker reads, IChangeApprover approver) : ITool
{
    public string Name => "Edit";

    public string Description =>
        "Replace exact text in an existing file.\n" +
        "- file_path: path to the file, absolute or relative to the working directory.\n" +
        "- old_string: the exact text to replace, copied from the file without the line-number prefix. " +
        "It must appear exactly once; include a few surrounding lines to make it unique.\n" +
        "- new_string: the replacement text (must differ from old_string).\n" +
        "- replace_all (optional): true to replace every occurrence.\n" +
        "You must Read the file (at least the part you change) before editing it.";

    public bool IsReadOnly => false;

    public AIFunction AsAIFunction() =>
        ToolFunction.Create(ExecuteAsync, Name, Description);

    // replace_all is nullable so the schema marks it optional; models otherwise invent a value.
#pragma warning disable CA1707
    internal async Task<string> ExecuteAsync(
        [Description("Path to the file, absolute or relative to the working directory.")] string file_path,
        [Description("Exact text to replace.")] string old_string,
        [Description("Replacement text (must differ from old_string).")] string new_string,
        [Description("Replace every occurrence. Omit for a single, unique occurrence.")] bool? replace_all = null,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        if (string.IsNullOrWhiteSpace(file_path))
            return "Error: file_path is required.";

        var oldText = Lf(old_string ?? "");
        var newText = Lf(new_string ?? "");
        if (oldText == newText)
            return "Error: no changes to make: old_string and new_string are exactly the same.";

        var fullPath = cwd.Resolve(file_path);
        if (!File.Exists(fullPath))
            return $"Error: file not found: {file_path}. To create a new file, use Write.";

        if (!reads.IsKnown(fullPath))
            return $"Error: the file has not been read this session. Recovery: call Read on '{fullPath}', then Edit again.";
        if (reads.HasChangedSinceRead(fullPath))
            return $"Error: the file has been modified since it was last read. Read it again before editing: {fullPath}";

        // Match on LF so CRLF files and LF old_strings still line up; write back in the file's own style.
        var raw = await File.ReadAllTextAsync(fullPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        var usesCrlf = raw.Contains("\r\n", StringComparison.Ordinal);
        var content = Lf(raw);

        string updated;
        if (oldText.Length == 0)
        {
            // Empty old_string fills an empty file; it never overwrites existing content.
            if (content.Trim().Length > 0)
                return "Error: old_string is empty but the file already has content. Use a non-empty old_string, or Write to replace the whole file.";
            updated = newText;
        }
        else
        {
            var matches = CountOccurrences(content, oldText);
            if (matches == 0)
                return NotFoundMessage(fullPath, oldText, content);
            if (matches > 1 && replace_all != true)
                return $"Error: found {matches} matches of old_string, but replace_all is not set. " +
                       "To replace all of them, set replace_all to true. To replace one, include more surrounding " +
                       $"lines so it matches exactly once.\nString: {oldText}";

            updated = replace_all == true
                ? content.Replace(oldText, newText, StringComparison.Ordinal)
                : ReplaceFirst(content, oldText, newText);
        }

        if (!await approver.ApproveAsync(new FileChange(cwd.Relative(fullPath), content, updated), cancellationToken).ConfigureAwait(false))
            return WriteTool.Declined(file_path);

        await AtomicFile.WriteAllTextAsync(fullPath, usesCrlf ? updated.Replace("\n", "\r\n", StringComparison.Ordinal) : updated, cancellationToken)
            .ConfigureAwait(false);

        // Our own change shouldn't trip "modified since read", but a partial read must not turn into a full one.
        reads.Refresh(fullPath);

        return replace_all == true
            ? $"The file {file_path} has been updated. All occurrences were replaced."
            : $"The file {file_path} has been updated successfully.";
    }

    // When old_string isn't in the file (usually stale after an earlier edit), show where the
    // nearest match is and how to re-read just that part, instead of a bare "not found".
    internal static string NotFoundMessage(string path, string oldText, string content)
    {
        var sb = new StringBuilder()
            .Append("Error: old_string not found in ").Append(path).Append(".\n\n")
            .Append("Tried to replace:\n").Append(oldText).Append('\n');

        var token = FirstToken(oldText);
        var lines = content.Split('\n');
        var hit = token.Length == 0 ? -1 : Array.FindIndex(lines, l => l.Contains(token, StringComparison.Ordinal));
        if (hit < 0)
        {
            sb.Append("\nNo similar line found. old_string may be invented, or the file may differ from what you remember. ")
              .Append("Read the whole file again: Read({\"file_path\":\"").Append(path).Append("\"})\n");
            return sb.ToString();
        }

        var from = Math.Max(0, hit - 2);
        var to = Math.Min(lines.Length - 1, hit + 2);
        sb.Append("\nClosest match in the current file (around line ").Append(hit + 1).Append("):\n");
        for (var i = from; i <= to; i++)
            sb.Append(i == hit ? ">>> " : "    ")
              .Append((i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(5)).Append("  ").Append(lines[i]).Append('\n');
        sb.Append("\nIf you edited this file earlier, its lines may have moved. Re-read that part with ")
          .Append("Read({\"file_path\":\"").Append(path).Append("\",\"offset\":").Append(from + 1)
          .Append(",\"limit\":").Append(to - from + 1).Append("}) and retry with the actual text.\n");
        return sb.ToString();
    }

    // First run of non-space characters, capped so a huge pasted block stays cheap to search.
    private static string FirstToken(string s)
    {
        var trimmed = s.TrimStart();
        var end = 0;
        while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end]))
            end++;
        return trimmed[..Math.Min(end, 60)];
    }

    private static string Lf(string s) => s.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static string ReplaceFirst(string haystack, string needle, string replacement)
    {
        var i = haystack.IndexOf(needle, StringComparison.Ordinal);
        return string.Concat(haystack.AsSpan(0, i), replacement, haystack.AsSpan(i + needle.Length));
    }
}
