using System.Text.Json;

namespace Adham.Cli;

// Show search results and shell status so the user can check the model's claims.
// The model still gets the full result; this is only the on-screen summary.
internal static class ToolResultText
{
    public static IReadOnlyList<string> Summarize(string toolName, object? result, int maxLines = 6)
    {
        if (toolName is not ("PowerShell" or "Bash" or "Grep"))
            return [];

        var text = result is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString() : result?.ToString();
        if (string.IsNullOrEmpty(text))
            return [];

        if (toolName == "Grep")
        {
            var searchLines = text.Split('\n');
            var shown = searchLines.Take(maxLines).ToList();
            if (searchLines.Length > maxLines)
                shown.Add("<more search output sent to the model>");
            return shown;
        }

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
