using System.Globalization;
using Adham.Core;

namespace Adham.Cli;

// The line after each model call that says how fast it was: how long the server took to read the
// prompt and how fast it wrote the reply (thinking included, which isn't shown).
internal static class TimingText
{
    public static string Format(ModelCallTiming timing)
    {
        var line = $"◷ {Seconds(timing.Total)}";

        if (timing.Server is { } server)
        {
            var cached = server.CachedTokens > 0 ? $", {Count(server.CachedTokens)} cached" : "";
            return $"{line} · read {Count(server.PromptTokens)} tokens in {Seconds(server.PromptTime)}{Speed(server.PromptTokens, server.PromptTime)}{cached}"
                + $" · wrote {Count(server.ReplyTokens)} in {Seconds(server.ReplyTime)}{Speed(server.ReplyTokens, server.ReplyTime)}";
        }

        // Without the server's numbers: the wait for the first token, and the reply speed after it.
        if (timing.FirstToken is { } first)
            line += $" · first token after {Seconds(first)}";
        if (timing.OutputTokens is { } output)
            line += $" · wrote {Count(output)}{Speed(output, timing.Total - (timing.FirstToken ?? TimeSpan.Zero))}";
        return line;
    }

    // After the turn: all of it, model calls, tools and approvals together.
    public static string Turn(TimeSpan elapsed, int modelCalls) =>
        $"turn {Seconds(elapsed)}, {modelCalls} model call{(modelCalls == 1 ? "" : "s")}";

    private static string Speed(long tokens, TimeSpan time) =>
        tokens > 0 && time.TotalSeconds > 0
            ? $" ({(tokens / time.TotalSeconds).ToString("N0", CultureInfo.InvariantCulture)} tok/s)"
            : "";

    private static string Seconds(TimeSpan time) =>
        time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";

    private static string Count(long tokens) =>
        tokens < 10_000
            ? tokens.ToString("N0", CultureInfo.InvariantCulture)
            : (tokens / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "K";
}
