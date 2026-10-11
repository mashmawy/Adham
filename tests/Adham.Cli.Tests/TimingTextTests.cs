using Adham.Cli;
using Adham.Core;
using FluentAssertions;
using Xunit;

namespace Adham.Cli.Tests;

public class TimingTextTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void WithServerTimings_ShowsReadingAndWritingSpeed()
    {
        var timing = new ModelCallTiming(S(2.9), S(0.4), 13_316, 87,
            new ServerTimings(PromptTokens: 1_204, PromptTime: S(0.3), CachedTokens: 12_112, ReplyTokens: 87, ReplyTime: S(2.6)));

        TimingText.Format(timing).Should().Be("◷ 2.9s · read 1,204 tokens in 0.3s (4,013 tok/s), 12.1K cached · wrote 87 in 2.6s (33 tok/s)");
    }

    [Fact]
    public void NothingCached_LeavesItOut()
    {
        var timing = new ModelCallTiming(S(1), S(0.2), 16, 60, new ServerTimings(16, S(0.26), 0, 60, S(1.87)));

        TimingText.Format(timing).Should().Be("◷ 1.0s · read 16 tokens in 0.3s (62 tok/s) · wrote 60 in 1.9s (32 tok/s)");
    }

    [Fact]
    public void WithoutServerTimings_UsesTheFirstTokenAndTheUsage()
    {
        var timing = new ModelCallTiming(S(3), S(1), 900, 100, Server: null);

        TimingText.Format(timing).Should().Be("◷ 3.0s · first token after 1.0s · wrote 100 (50 tok/s)");
    }

    [Fact]
    public void WithoutUsage_ShowsTheTimeAlone()
    {
        TimingText.Format(new ModelCallTiming(S(3), null, null, null, null)).Should().Be("◷ 3.0s");
    }

    [Theory]
    [InlineData(1, "turn 41.2s, 1 model call")]
    [InlineData(6, "turn 41.2s, 6 model calls")]
    public void Turn_ShowsTheWholeTimeAndTheCalls(int calls, string expected)
    {
        TimingText.Turn(S(41.2), calls).Should().Be(expected);
    }
}
