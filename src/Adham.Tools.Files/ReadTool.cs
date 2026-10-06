using System.ComponentModel;
using System.Globalization;
using System.Text;
using Adham.Tools.Abstractions;
using Microsoft.Extensions.AI;

namespace Adham.Tools.Files;

public sealed class ReadTool(WorkingDirectory cwd, FileReadTracker? reads = null) : ITool
{
    internal const int MaxLines = 2000;
    internal const int MaxLineLength = 2000;
    private const int BinarySniffBytes = 8192;

    public string Name => "Read";

    public string Description =>
        "Read a text file and return its contents with line numbers (\"    12→code\").\n" +
        "- file_path: path to the file, absolute or relative to the working directory.\n" +
        "- offset / limit (optional): 1-based first line and number of lines, for large files.\n" +
        $"Returns at most {MaxLines} lines per call. Binary files return a placeholder; don't retry them.";

    public bool IsReadOnly => true;

    public AIFunction AsAIFunction() =>
        ToolFunction.Create(ExecuteAsync, Name, Description);

    // Parameter names are snake_case on purpose: they become the JSON schema the model sees.
#pragma warning disable CA1707
    internal async Task<string> ExecuteAsync(
        [Description("Path to the file, absolute or relative to the working directory.")] string file_path,
        [Description("1-based line to start from. Omit to start at line 1.")] int? offset = null,
        [Description("Maximum number of lines to return. Omit to read the whole file.")] int? limit = null,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        if (string.IsNullOrWhiteSpace(file_path))
            return "Error: file_path is required.";

        var fullPath = cwd.Resolve(file_path);
        if (Directory.Exists(fullPath))
            return $"Error: {file_path} is a folder. Use Glob to list its files.";
        if (!File.Exists(fullPath))
            return $"Error: file not found: {file_path} (resolved to {fullPath}).";

        if (await IsBinaryAsync(fullPath, cancellationToken).ConfigureAwait(false))
            return "<binary file: not shown>";

        var lines = await File.ReadAllLinesAsync(fullPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        // Remember the read so Edit/Write can check the model has seen the file.
        if (offset is null && limit is null)
            reads?.RecordFull(fullPath);
        else
            reads?.RecordPartial(fullPath);

        if (lines.Length == 0)
            return "<file is empty>";

        var start = offset is > 0 ? offset.Value : 1;
        if (start > lines.Length)
            return $"<offset {start} is past the end: the file has {lines.Length} lines>";

        var wanted = limit is > 0 ? limit.Value : MaxLines;
        var take = Math.Min(Math.Min(wanted, MaxLines), lines.Length - start + 1);

        var sb = new StringBuilder();
        for (var i = 0; i < take; i++)
        {
            var n = start + i;
            var line = lines[n - 1];
            if (line.Length > MaxLineLength)
                line = string.Concat(line.AsSpan(0, MaxLineLength), "…[truncated]");
            sb.Append(n.ToString(CultureInfo.InvariantCulture).PadLeft(6)).Append('→').Append(line).Append('\n');
        }

        var end = start + take - 1;
        if (end < lines.Length)
            sb.Append(CultureInfo.InvariantCulture, $"<showing lines {start}-{end} of {lines.Length}; use offset to read more>");

        return sb.ToString().TrimEnd('\n');
    }

    // Null bytes, or more than 10% control characters in the first 8 KB, means binary.
    private static async Task<bool> IsBinaryAsync(string path, CancellationToken ct)
    {
        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[BinarySniffBytes];
            var read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);

            var control = 0;
            for (var i = 0; i < read; i++)
            {
                var b = buffer[i];
                if (b == 0) return true;
                if (b < 32 && b is not (9 or 10 or 13)) control++;
            }
            return control > read / 10;
        }
    }
}
