using System.Text.Json;

namespace Adham.Core;

// How long one model call took, measured here (Total, FirstToken) and, when the server says,
// how the time split between reading the prompt and writing the reply.
public sealed record ModelCallTiming(
    TimeSpan Total,
    TimeSpan? FirstToken,
    long? InputTokens,
    long? OutputTokens,
    ServerTimings? Server);

// llama.cpp's "timings" block, which Unsloth Studio sends with the last chunk of a reply.
// PromptTokens are the ones the server had to read; CachedTokens were reused from the previous call.
// ReplyTokens include the model's thinking, which isn't shown.
public sealed record ServerTimings(long PromptTokens, TimeSpan PromptTime, long CachedTokens, long ReplyTokens, TimeSpan ReplyTime)
{
    // From the raw JSON of a chunk; null when it has no (readable) timings.
    public static ServerTimings? Parse(string chunkJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(chunkJson);
            if (!doc.RootElement.TryGetProperty("timings", out var t) || t.ValueKind != JsonValueKind.Object)
                return null;
            return new ServerTimings(
                Long(t, "prompt_n"),
                TimeSpan.FromMilliseconds(Double(t, "prompt_ms")),
                Long(t, "cache_n"),
                Long(t, "predicted_n"),
                TimeSpan.FromMilliseconds(Double(t, "predicted_ms")));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static long Long(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : 0;

    private static double Double(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.TryGetDouble(out var n) ? n : 0;
}
