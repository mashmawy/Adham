using Adham.Cli;
using FluentAssertions;
using Xunit;

namespace Adham.Cli.Tests;

public class ContextTextTests
{
    [Fact]
    public void ShowsUsedOfTheWindow()
    {
        ContextText.Format(21_300, 32_768).Should().Be("context 21.3K / 32.8K (65%)");
    }

    [Fact]
    public void WarnsFromThreeQuartersFull()
    {
        ContextText.Format(24_576, 32_768).Should().Be("context 24.6K / 32.8K (75%): nearly full, /clear soon");
    }

    [Fact]
    public void BelowThreeQuarters_DoesNotWarn()
    {
        ContextText.Format(24_000, 32_768).Should().NotContain("nearly full");
    }

    [Fact]
    public void UnknownWindow_ShowsTheCountAlone()
    {
        ContextText.Format(850, window: null).Should().Be("context 850 tokens");
    }

    [Fact]
    public void NoCount_ShowsNothing()
    {
        ContextText.Format(used: null, 32_768).Should().BeNull();
    }
}
