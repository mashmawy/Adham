using Adham.Cli;
using FluentAssertions;
using Xunit;

namespace Adham.Cli.Tests;

public class DiffTextTests
{
    private static string Render(string? before, string after, int maxLines = 60) =>
        string.Join('\n', DiffText.Lines(before, after, maxLines: maxLines).Select(l => $"{l.Kind} {l.Text}"));

    [Fact]
    public void ChangedLine_ShowsRemovedAndAdded_WithTwoLinesOfContext()
    {
        var before = "a\nb\nc\nd\ne\nf\ng\n";
        var after = "a\nb\nc\nD\ne\nf\ng\n";

        Render(before, after).Should().Be("  b\n  c\n- d\n+ D\n  e\n  f");
    }

    [Fact]
    public void FarApartChanges_AreSeparatedByAGapMarker()
    {
        var before = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"line {i}"));
        var after = before.Replace("line 2", "LINE 2", StringComparison.Ordinal)
                          .Replace("line 18", "LINE 18", StringComparison.Ordinal);

        var lines = DiffText.Lines(before, after);

        lines.Should().ContainSingle(l => l.Kind == '⋮');
    }

    [Fact]
    public void NewFile_IsAllAdditions()
    {
        Render(null, "one\ntwo").Should().Be("+ one\n+ two");
    }

    [Fact]
    public void LongDiff_IsCapped_WithACountOfWhatIsHidden()
    {
        var after = string.Join('\n', Enumerable.Range(1, 100).Select(i => $"line {i}"));

        var lines = DiffText.Lines(null, after, maxLines: 10);

        lines.Should().HaveCount(11);
        lines[^1].Should().Be(('⋮', "… 90 more lines"));
    }
}
