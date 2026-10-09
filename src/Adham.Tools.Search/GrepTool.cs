using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Adham.Tools.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Adham.Tools.Search;

public sealed class GrepTool(WorkingDirectory cwd) : ITool
{
    internal const int MaxResults = 100;
    internal const int DefaultLimit = 20;
    internal const int MaxLineLength = 500;
    internal const int MaxOutputChars = 12000;
    internal const string NoMatchesMessage =
        "No matches found. Before asking the user, retry with only the distinctive identifier or term from their question " +
        "(drop generic words such as decision, feature or configuration). Use ignore_case: true if casing is uncertain. " +
        "A failed phrase search does not mean its individual terms are absent.";

    public string Name => "Grep";
    public bool IsReadOnly => true;
    public string Description =>
        "Search text file contents using a .NET regular expression, one line at a time. " +
        "Returns working-directory-relative path:line:content matches.\n" +
        "Answer from these excerpts when sufficient; otherwise Read only a small offset/limit range around a match. " +
        "Start with one distinctive term or identifier, not generic words such as decision. " +
        "Patterns combining words only match when those words occur on the same line. " +
        "If there are no matches, simplify the pattern before concluding the topic is absent.\n" +
        "- pattern: required regex; matching is case-sensitive by default.\n" +
        "- path (optional): file or folder; defaults to the working directory.\n" +
        "- glob (optional): filter file paths relative to the search folder, e.g. **/*.<ext>.\n" +
        "- ignore_case (optional): ignore letter case.\n" +
        "- whole_word (optional): defaults to true, so CI won't match inside decision. Set false for substring searches.\n" +
        $"- limit (optional): maximum matching lines; defaults to {DefaultLimit}, maximum {MaxResults}.\n" +
        "- context (optional): nearby lines before/after each match, 0 by default, maximum 5. Overlapping excerpts are merged.\n" +
        $"Output is capped at {MaxOutputChars} characters plus a truncation notice; lines are clipped to {MaxLineLength} characters. " +
        "Skips binary files, symbolic links, bin, obj, .git and node_modules. Use Read before editing a match.";

    public AIFunction AsAIFunction() => ToolFunction.Create(ExecuteAsync, Name, Description);

#pragma warning disable CA1707
    internal async Task<string> ExecuteAsync(
        [Description("Regular expression to search for in file contents.")] string pattern,
        [Description("File or folder to search. Omit for the working directory.")] string? path = null,
        [Description("Optional file glob, e.g. **/*.<ext>, relative to the search folder.")] string? glob = null,
        [Description("Ignore letter case. Defaults to false.")] bool ignore_case = false,
        [Description("Match whole words. Defaults to true; false allows substring matches.")] bool whole_word = true,
        [Description("Maximum matching lines. Default 20, range 1-100.")] int limit = DefaultLimit,
        [Description("Lines before and after matches. Default 0, range 0-5.")] int context = 0,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return "Error: pattern is required.";
        if (limit is < 1 or > MaxResults)
            return $"Error: limit must be between 1 and {MaxResults}.";
        if (context is < 0 or > 5)
            return "Error: context must be between 0 and 5.";

        Regex regex;
        try
        {
            var expression = whole_word ? @"(?<!\w)(?:" + pattern + @")(?!\w)" : pattern;
            regex = new Regex(expression, RegexOptions.CultureInvariant | (ignore_case ? RegexOptions.IgnoreCase : RegexOptions.None),
                TimeSpan.FromMilliseconds(250));
        }
        catch (ArgumentException)
        {
            return "Error: invalid regular expression.";
        }

        var root = string.IsNullOrWhiteSpace(path) ? cwd.Path : cwd.Resolve(path);
        var singleFile = File.Exists(root);
        if (!singleFile && !Directory.Exists(root))
            return $"Error: path not found: {path}";

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(string.IsNullOrWhiteSpace(glob) ? "**/*" : glob);
        var results = new List<string>();
        var matches = 0;
        var outputChars = 0;
        var skipped = 0;
        string Truncated(string reason) => string.Join('\n', results) + $"\n<{reason}; narrow the pattern or path, or Read a small offset/limit range around a match>";

        bool AddLine(string file, int number, string line)
        {
            var text = line.Length <= MaxLineLength ? line : line[..MaxLineLength] + "…[truncated]";
            var formatted = string.Create(CultureInfo.InvariantCulture, $"{cwd.Relative(file)}:{number}:{text}");
            if (outputChars + formatted.Length + 1 > MaxOutputChars) return false;
            results.Add(formatted);
            outputChars += formatted.Length + 1;
            return true;
        }
        try
        {
            var files = singleFile ? [root] : EnumerateFiles(root, cancellationToken);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    continue;
                var relative = singleFile ? Path.GetFileName(file) : Path.GetRelativePath(root, file);
                if (!matcher.Match(relative.Replace('\\', '/')).HasMatches)
                    continue;

                try
                {
                    var buffer = new char[8192];
                    using var probe = File.OpenText(file);
                    var count = await probe.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    // Inspect decoded text so UTF-16 files with a BOM remain searchable.
                    if (buffer.AsSpan(0, count).Contains('\0'))
                        continue;
                    using var reader = File.OpenText(file);
                    var lineNumber = 0;
                    var lastShown = 0;
                    var showThrough = 0;
                    var preceding = new Queue<(int Number, string Text)>();
                    while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        lineNumber++;
                        if (regex.IsMatch(line))
                        {
                            if (matches == limit)
                                return Truncated("more matches not shown");
                            matches++;
                            foreach (var previous in preceding)
                            {
                                if (previous.Number <= lastShown) continue;
                                if (!AddLine(file, previous.Number, previous.Text)) return Truncated("output limit reached");
                                lastShown = previous.Number;
                            }
                            showThrough = lineNumber + context;
                        }
                        if (lineNumber <= showThrough)
                        {
                            if (!AddLine(file, lineNumber, line)) return Truncated("output limit reached");
                            lastShown = lineNumber;
                        }
                        if (context > 0)
                        {
                            preceding.Enqueue((lineNumber, line.Length <= MaxLineLength ? line : line[..MaxLineLength] + "…[truncated]"));
                            if (preceding.Count > context) preceding.Dequeue();
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    skipped++;
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return "Error: regular expression timed out; simplify the pattern or narrow the path.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "Error: could not complete search: " + ex.Message;
        }

        var output = results.Count == 0 ? NoMatchesMessage : string.Join('\n', results);
        return skipped == 0 ? output : output + $"\n<{skipped} unreadable files skipped>";
    }

    private static IEnumerable<string> EnumerateFiles(string root, CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        foreach (var entry in Directory.EnumerateFileSystemEntries(root, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(entry))
            {
                var name = Path.GetFileName(entry);
                if (name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
                    || name.Equals(".git", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var file in EnumerateFiles(entry, cancellationToken))
                    yield return file;
            }
            else
            {
                yield return entry;
            }
        }
    }
}
