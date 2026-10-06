using System.Text.Json;
using Adham.Tools.Abstractions;

namespace Adham.Cli;

// The grey "⚙ Tool(args)" line. Models often send absolute paths; inside the working
// directory those are shown relative, so the line stays readable.
internal static class ToolCallText
{
    public static string Format(string name, IDictionary<string, object?>? args, WorkingDirectory cwd) =>
        args is null
            ? $"{name}()"
            : $"{name}({string.Join(", ", args.Select(a => $"{a.Key}: {Shorten(Text(a.Value), cwd)}"))})";

    private static string? Text(object? value) =>
        value is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString() : value?.ToString();

    private static string? Shorten(string? value, WorkingDirectory cwd)
    {
        if (string.IsNullOrEmpty(value) || !Path.IsPathFullyQualified(value))
            return value;

        var relative = cwd.Relative(Path.GetFullPath(value));
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative) ? value : relative;
    }
}
