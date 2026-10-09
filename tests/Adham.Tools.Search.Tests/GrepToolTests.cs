using System.Text;
using Adham.Tools.Abstractions;
using Adham.Tools.Search;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Tools.Search.Tests;

public sealed class GrepToolTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("adham-grep-").FullName;
    private readonly GrepTool _tool;

    public GrepToolTests() => _tool = new GrepTool(new WorkingDirectory(_dir));
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Write(string path, string text)
    {
        var full = Path.Combine(_dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    [Fact]
    public async Task DocumentLabel_IsFoundAlone_WhenDescriptionIsOnAnotherLine()
    {
        Write("review.md", "| # | Decision |\n|---|---|\n| C1 | The parser receives a file upload. |");
        (await _tool.ExecuteAsync("c1.*decision|decision.*c1", ignore_case: true)).Should().Be(GrepTool.NoMatchesMessage);
        (await _tool.ExecuteAsync("C1Decision|c1_decision|C1_DECISION", ignore_case: true)).Should().Be(GrepTool.NoMatchesMessage);
        (await _tool.ExecuteAsync("c1", ignore_case: true)).Should().Be("review.md:3:| C1 | The parser receives a file upload. |");
    }

    [Fact]
    public async Task SearchesRecursively_WithRegexAndLineNumbers()
    {
        Write("src/file.txt", "first\nitem123\nitemABC\nitem456");
        (await _tool.ExecuteAsync(@"item\d+")).Should().Be("src/file.txt:2:item123\nsrc/file.txt:4:item456");
    }

    [Fact]
    public async Task PathGlobAndCaseOptions_FilterMatches()
    {
        Write("a/file.txt", "HELLO");
        Write("a/file.log", "HELLO");
        Write("b/file.txt", "HELLO");
        (await _tool.ExecuteAsync("hello")).Should().Be(GrepTool.NoMatchesMessage);
        (await _tool.ExecuteAsync("hello", "a", "**/*.txt", true)).Should().Be("a/file.txt:1:HELLO");
        (await _tool.ExecuteAsync("HELLO", "b/file.txt")).Should().Be("b/file.txt:1:HELLO");
    }

    [Fact]
    public async Task SkipsBinaryAndExcludedFolders_ButSearchesHiddenFiles()
    {
        foreach (var dir in new[] { "bin", "nested/obj", ".git", "node_modules" })
            Write(dir + "/file.txt", "needle");
        Write("binary.dat", "needle\0data");
        Write(".hidden", "needle");
        (await _tool.ExecuteAsync("needle")).Should().Be(".hidden:1:needle");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlesBomAndNonAsciiText(bool utf16)
    {
        File.WriteAllText(Path.Combine(_dir, "file.txt"), "مرحبا\r\nworld", utf16 ? Encoding.Unicode : new UTF8Encoding(true));
        (await _tool.ExecuteAsync("^مرحبا$|^world$")).Should().Be("file.txt:1:مرحبا\nfile.txt:2:world");
    }

    [Theory]
    [InlineData("", "Error: pattern is required.")]
    [InlineData("[", "Error: invalid regular expression.")]
    public async Task RejectsInvalidPatterns(string pattern, string expected)
    {
        (await _tool.ExecuteAsync(pattern)).Should().Be(expected);
    }

    [Fact]
    public async Task ReportsMissingPathAndNoMatches()
    {
        (await _tool.ExecuteAsync("x", "missing")).Should().StartWith("Error: path not found:");
        (await _tool.ExecuteAsync("x")).Should().Be(GrepTool.NoMatchesMessage);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public async Task CapsMatchingLines_AndOnlyReportsTruncationWhenNeeded(int count, bool truncated)
    {
        Write("file.txt", string.Join('\n', Enumerable.Repeat("match", count)));
        var result = await _tool.ExecuteAsync("match", limit: GrepTool.MaxResults);
        result.Split('\n').Count(l => l.StartsWith("file.txt:", StringComparison.Ordinal)).Should().Be(GrepTool.MaxResults);
        result.Contains("more matches", StringComparison.Ordinal).Should().Be(truncated);
    }

    [Fact]
    public async Task ClipsLongMatchingLines()
    {
        Write("file.txt", new string('x', GrepTool.MaxLineLength + 10));
        (await _tool.ExecuteAsync("x", whole_word: false)).Should().Be("file.txt:1:" + new string('x', GrepTool.MaxLineLength) + "…[truncated]");
    }

    [Fact]
    public async Task WholeWords_DefaultAvoidsSubstringNoise_AndCanBeDisabled()
    {
        Write("review.md", "Decision\nprecision\nNo live tests in CI.\nC1\nC10");
        (await _tool.ExecuteAsync("ci", ignore_case: true)).Should().Be("review.md:3:No live tests in CI.");
        (await _tool.ExecuteAsync("C1")).Should().Be("review.md:4:C1");
        (await _tool.ExecuteAsync("ci", ignore_case: true, whole_word: false)).Split('\n').Should().HaveCount(3);
    }

    [Fact]
    public async Task WholeWordAlternation_ConstrainsEveryAlternative()
    {
        Write("review.md", "Decision\nCI\nC1\nC10\nXC1");
        (await _tool.ExecuteAsync("CI|C1", ignore_case: true)).Should().Be("review.md:2:CI\nreview.md:3:C1");
    }

    [Fact]
    public async Task ContextMergesOverlappingRanges_AndIncludesFileEdges()
    {
        Write("review.md", "match\nbefore\nmatch\nafter\nlast");
        (await _tool.ExecuteAsync("match", context: 2)).Should().Be(
            "review.md:1:match\nreview.md:2:before\nreview.md:3:match\nreview.md:4:after\nreview.md:5:last");
    }

    [Fact]
    public async Task ContextDoesNotLeakBetweenFiles()
    {
        Write("a.md", "unrelated");
        Write("b.md", "match\nafter");
        (await _tool.ExecuteAsync("match", context: 2)).Should().Be("b.md:1:match\nb.md:2:after");
    }

    [Fact]
    public async Task LimitCountsMatchesInsteadOfContextLines()
    {
        Write("review.md", "before\nmatch\nafter\ngap\nmatch");
        var result = await _tool.ExecuteAsync("match", limit: 1, context: 1);
        result.Should().StartWith("review.md:1:before\nreview.md:2:match\nreview.md:3:after\n<more matches not shown;")
            .And.NotContain("review.md:5:");
    }

    [Fact]
    public async Task DefaultLimitKeepsBroadSearchesSmall()
    {
        Write("file.txt", string.Join('\n', Enumerable.Repeat("match", 21)));
        var result = await _tool.ExecuteAsync("match");
        result.Split('\n').Count(l => l.StartsWith("file.txt:", StringComparison.Ordinal)).Should().Be(GrepTool.DefaultLimit);
        result.Should().Contain("more matches not shown");
    }

    [Fact]
    public async Task CharacterBudgetBoundsEvenLongLinesWithContext()
    {
        Write("file.txt", string.Join('\n', Enumerable.Repeat("match " + new string('x', 600), 100)));
        var result = await _tool.ExecuteAsync("match", limit: 100, context: 5);
        result.Should().Contain("output limit reached");
        result[..result.IndexOf("\n<output limit", StringComparison.Ordinal)].Length.Should().BeLessThanOrEqualTo(GrepTool.MaxOutputChars);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(101, 0)]
    [InlineData(5, -1)]
    [InlineData(5, 6)]
    public async Task RejectsInvalidOutputOptions(int limit, int context)
    {
        (await _tool.ExecuteAsync("match", limit: limit, context: context)).Should().StartWith("Error:");
    }

    [Fact]
    public async Task HonorsCancellation()
    {
        Write("file.txt", "match");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var action = () => _tool.ExecuteAsync("match", cancellationToken: cts.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FunctionCanBeInvokedWithModelArguments()
    {
        Write("file.txt", "HELLO");
        var function = _tool.AsAIFunction();
        function.Name.Should().Be("Grep");
        _tool.IsReadOnly.Should().BeTrue();
        var result = await function.InvokeAsync(new AIFunctionArguments
        {
            ["pattern"] = "hello", ["ignore_case"] = true, ["whole_word"] = true, ["limit"] = 5, ["context"] = 2,
        });
        result.Should().Be("file.txt:1:HELLO");
    }
}
