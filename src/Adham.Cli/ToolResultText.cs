using System.Text.Json;

namespace Adham.Cli;

// What the user sees of a shell command's result: the exit code and the last few output lines,
// so "all tests pass" is something they can check, not just something the model says.
// The model still gets the full result; this is only the on-screen summary.
internal static class ToolResultText
{
    public static IReadOnlyList<string> Summarize(string toolName, object? result, int maxLines = 6)
    {
        if (toolName is not ("PowerShell" or "Bash"))
            return [];

        var text = result is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString() : result?.ToString();
        if (string.IsNullOrEmpty(text))
            return [];

        if (text.StartsWith("Refused: ", StringComparison.Ordinal))
            return ["refused: " + text["Refused: ".Length..].Split(". ")[0]];
        if (!text.Contains("\nexit_code=", StringComparison.Ordinal))
            return [];   // declined or an argument error: the user already saw the prompt

        var lines = text.Split('\n');
        if (Array.Find(lines, l => l.StartsWith("status=timeout", StringComparison.Ordinal)) is { } timeout)
            return ["timed out" + timeout["status=timeout".Length..]];

        var exit = Array.Find(lines, l => l.StartsWith("exit_code=", StringComparison.Ordinal))!["exit_code=".Length..];
        var output = lines
            .SkipWhile(l => l != "stdout:").Skip(1)
            .Where(l => l != "stderr:" && !l.StartsWith("[The command failed", StringComparison.Ordinal))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .TakeLast(maxLines);

        return [exit == "0" ? "exit 0" : $"exit {exit} (failed)", .. output];
    }
}
