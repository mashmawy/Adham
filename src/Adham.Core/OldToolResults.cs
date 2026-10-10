using Microsoft.Extensions.AI;

namespace Adham.Core;

// Every model call re-sends the whole conversation, so a file read once stays in the context for good:
// measured at about 2K tokens per medium file, until a 32K window is full, sometimes within one turn.
// This makes the copy that is sent: the newest tool rounds stay whole while they fit a budget, and
// long results in older rounds become a one-line note that says what ran and how to get the text back.
// The history itself is never changed.
internal static class OldToolResults
{
    // Results up to this long stay as they are: they cost little, and short ones ("ok", an exit code,
    // an error) are often what the model needs to remember.
    internal const int KeepUpToChars = 500;

    public static IList<ChatMessage> Shorten(IList<ChatMessage> messages, int keepChars)
    {
        // A round starts at an assistant message that calls tools; its results follow it.
        var roundStarts = Enumerable.Range(0, messages.Count)
            .Where(i => messages[i].Role == ChatRole.Assistant && messages[i].Contents.OfType<FunctionCallContent>().Any())
            .ToList();

        // Newest first: the latest round always stays whole, then older ones while they fit.
        // The first round that doesn't fit ends it, so a newer round is never shortened before an older one.
        var shortenBefore = 0;
        var kept = 0;
        for (var r = roundStarts.Count - 1; r >= 0; r--)
        {
            var end = r + 1 < roundStarts.Count ? roundStarts[r + 1] : messages.Count;
            kept += Size(messages, roundStarts[r], end);
            if (kept > keepChars && r < roundStarts.Count - 1)
            {
                shortenBefore = end;
                break;
            }
        }

        if (shortenBefore == 0)
            return messages;

        var calls = messages.Take(shortenBefore)
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .GroupBy(c => c.CallId)
            .ToDictionary(g => g.Key, g => g.First());

        var sent = new List<ChatMessage>(messages.Count);
        for (var i = 0; i < messages.Count; i++)
            sent.Add(i < shortenBefore ? ShortenMessage(messages[i], calls) : messages[i]);
        return sent;
    }

    // What a round adds to the context: its results, and its call arguments (a Write's whole file).
    private static int Size(IList<ChatMessage> messages, int start, int end) =>
        messages.Skip(start).Take(end - start).SelectMany(m => m.Contents).Sum(content => content switch
        {
            FunctionResultContent result => Text(result.Result)?.Length ?? 0,
            FunctionCallContent call => call.Arguments?.Sum(a => Text(a.Value)?.Length ?? 0) ?? 0,
            _ => 0,
        });

    // A new message (the original belongs to the history) with long results and arguments replaced by notes.
    private static ChatMessage ShortenMessage(ChatMessage message, Dictionary<string, FunctionCallContent> calls)
    {
        if (!message.Contents.Any(IsLong))
            return message;

        var contents = message.Contents.Select(content => content switch
        {
            FunctionResultContent result when IsLong(result) =>
                new FunctionResultContent(result.CallId, Note(calls.GetValueOrDefault(result.CallId), Text(result.Result)!)),
            FunctionCallContent call when IsLong(call) =>
                new FunctionCallContent(call.CallId, call.Name, call.Arguments!.ToDictionary(a => a.Key, a => ShortenArgument(a.Value))),
            _ => content,
        }).ToList();
        return new ChatMessage(message.Role, contents) { AuthorName = message.AuthorName, MessageId = message.MessageId };
    }

    private static bool IsLong(AIContent content) => content switch
    {
        FunctionResultContent result => Text(result.Result) is { Length: > KeepUpToChars },
        FunctionCallContent { Arguments: { } args } => args.Values.Any(v => Text(v) is { Length: > KeepUpToChars }),
        _ => false,
    };

    // The call already ran, so a long argument (a file written, an edit's text) is on disk now.
    private static object? ShortenArgument(object? value) =>
        Text(value) is { Length: > KeepUpToChars } text
            ? $"[{text.Count(c => c == '\n') + 1} lines, removed to save context]"
            : value;

    internal static string Note(FunctionCallContent? call, string text)
    {
        var lines = text.Count(c => c == '\n') + 1;
        if (call is null)
            return $"[Earlier tool result ({lines} lines) removed to save context.]";

        var args = call.Arguments is null ? "" : string.Join(", ", call.Arguments.Select(a => $"{a.Key}: {Clip(a.Value?.ToString())}"));
        return $"[Earlier result of {call.Name}({args}): {lines} lines, removed to save context. Call {call.Name} again if you need it.]";
    }

    internal static string? Text(object? result) => result switch
    {
        null => null,
        string s => s,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } json => json.GetString(),
        _ => result.ToString(),
    };

    private static string Clip(string? value) =>
        value is null ? "null" : value.Length <= 80 ? value : string.Concat(value.AsSpan(0, 80), "…");
}
