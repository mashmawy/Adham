using System.Globalization;

namespace Adham.Cli;

// The line after each turn that says how full the model's context is, so a full context
// is seen coming instead of showing up as an empty reply.
internal static class ContextText
{
    // From here on the user is told to /clear soon: past this, a turn that reads a file or two can fill it.
    internal const double WarnAt = 0.75;

    public static string? Format(long? used, int? window)
    {
        if (used is not { } tokens)
            return null;
        if (window is not { } size)
            return $"context {Short(tokens)} tokens";

        var share = (double)tokens / size;
        var line = $"context {Short(tokens)} / {Short(size)} ({(int)Math.Round(share * 100)}%)";
        return share >= WarnAt ? $"{line}: nearly full, /clear soon" : line;
    }

    private static string Short(long tokens) =>
        tokens < 1000
            ? tokens.ToString(CultureInfo.InvariantCulture)
            : (tokens / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "K";
}
