using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace Adham.Cli;

// A compact line diff for the approval prompt: changed lines plus a little context,
// '⋮' between distant changes, and a cap so a big file doesn't flood the terminal.
internal static class DiffText
{
    public static IReadOnlyList<(char Kind, string Text)> Lines(string? before, string after, int context = 2, int maxLines = 60)
    {
        var diff = InlineDiffBuilder.Diff(before ?? "", after, ignoreWhiteSpace: false, ignoreCase: false).Lines;
        var kinds = diff.Select(l => l.Type switch
        {
            ChangeType.Inserted => '+',
            ChangeType.Deleted => '-',
            _ => ' ',
        }).ToArray();

        // Keep every changed line and `context` unchanged lines on each side of it.
        var keep = new bool[diff.Count];
        for (var i = 0; i < diff.Count; i++)
        {
            if (kinds[i] == ' ')
                continue;
            for (var j = Math.Max(0, i - context); j <= Math.Min(diff.Count - 1, i + context); j++)
                keep[j] = true;
        }

        var result = new List<(char, string)>();
        var lastKept = -1;
        for (var i = 0; i < diff.Count; i++)
        {
            if (!keep[i])
                continue;
            if (lastKept >= 0 && i > lastKept + 1)
                result.Add(('⋮', ""));
            result.Add((kinds[i], diff[i].Text));
            lastKept = i;
        }

        if (result.Count > maxLines)
        {
            var hidden = result.Count - maxLines;
            result = result.Take(maxLines).ToList();
            result.Add(('⋮', $"… {hidden} more lines"));
        }
        return result;
    }
}
