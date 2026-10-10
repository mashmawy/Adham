using Microsoft.Extensions.AI;

namespace Adham.Core;

// Every turn re-sends the whole conversation, so a file read once stays in the context for good:
// measured at about 2K tokens per medium file, until a 32K window is full. Old, long tool results
// are replaced by a one-line note that says what ran and how to get the text back.
internal static class OldToolResults
{
    // Results up to this long stay as they are: they cost little, and short ones ("ok", an exit code,
    // an error) are often what the model needs to remember.
    internal const int KeepUpToChars = 500;

    // Replaces the long tool results in messages[0..end) with a note. Notes are short, so this is idempotent.
    public static void Shorten(IList<ChatMessage> messages, int end)
    {
        var calls = messages.Take(end)
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .GroupBy(c => c.CallId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var message in messages.Take(end))
        {
            for (var i = 0; i < message.Contents.Count; i++)
            {
                if (message.Contents[i] is not FunctionResultContent result || Text(result.Result) is not { Length: > KeepUpToChars } text)
                    continue;

                calls.TryGetValue(result.CallId, out var call);
                message.Contents[i] = new FunctionResultContent(result.CallId, Note(call, text));
            }
        }
    }

    internal static string Note(FunctionCallContent? call, string text)
    {
        var lines = text.Count(c => c == '\n') + 1;
        if (call is null)
            return $"[Earlier tool result ({lines} lines) removed to save context.]";

        var args = call.Arguments is null ? "" : string.Join(", ", call.Arguments.Select(a => $"{a.Key}: {Clip(a.Value?.ToString())}"));
        return $"[Earlier result of {call.Name}({args}): {lines} lines, removed to save context. Call {call.Name} again if you need it.]";
    }

    private static string? Text(object? result) => result switch
    {
        null => null,
        string s => s,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } json => json.GetString(),
        _ => result.ToString(),
    };

    private static string Clip(string? value) =>
        value is null ? "null" : value.Length <= 80 ? value : string.Concat(value.AsSpan(0, 80), "…");
}
