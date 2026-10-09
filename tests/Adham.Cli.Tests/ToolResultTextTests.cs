using System.Text.Json;
using Adham.Cli;
using FluentAssertions;
using Xunit;

namespace Adham.Cli.Tests;

public class ToolResultTextTests
{
    // What the shell tool returned to the model in the live demo (python -m unittest writes to stderr).
    private const string UnittestOk =
        "PS> python -m unittest -v\nexit_code=0\nstdout:\nstderr:\ntest_a ... ok\ntest_b ... ok\n\n" +
        "----------------------------------------------------------------------\nRan 6 tests in 0.001s\n\nOK";

    [Fact]
    public void ShellSuccess_ShowsExitCodeAndTheLastLines()
    {
        var lines = ToolResultText.Summarize("PowerShell", UnittestOk, maxLines: 3);

        lines.Should().Equal("exit 0", "----------------------------------------------------------------------", "Ran 6 tests in 0.001s", "OK");
    }

    [Fact]
    public void ShellFailure_SaysFailed_AndDropsTheModelOnlyNote()
    {
        const string result = "$ pytest\nexit_code=1\nstdout:\n1 failed, 5 passed\nstderr:\n" +
                              "[The command failed. Fix the command and retry, or report the failure to the user. Don't ask the user for new instructions.]";

        ToolResultText.Summarize("Bash", result).Should().Equal("exit 1 (failed)", "1 failed, 5 passed");
    }

    [Fact]
    public void Timeout_IsShown()
    {
        const string result = "$ sleep 999\nexit_code=-1\nstatus=timeout (the process was stopped)\nstdout:\nstderr:";

        ToolResultText.Summarize("Bash", result).Should().Equal("timed out (the process was stopped)");
    }

    [Fact]
    public void Refusal_IsShown_BecauseOnlyTheModelWouldSeeItOtherwise()
    {
        const string result = "Refused: git push --force (rewrites shared history). This command is blocked and can't be approved. Don't try…";

        ToolResultText.Summarize("PowerShell", result).Should().Equal("refused: git push --force (rewrites shared history)");
    }

    [Fact]
    public void Declined_ShowsNothing_TheUserAlreadyAnswered()
    {
        ToolResultText.Summarize("Bash", "The user declined to run this command. Don't retry it; ask the user what they want instead.")
            .Should().BeEmpty();
    }

    [Fact]
    public void Grep_ShowsMatchesWithABoundedPreview()
    {
        ToolResultText.Summarize("Grep", "review.md:1:## C1\nreview.md:8:C1 details", maxLines: 1)
            .Should().Equal("review.md:1:## C1", "<more search output sent to the model>");
    }

    [Theory]
    [InlineData("No matches found.")]
    [InlineData("Error: invalid regular expression.")]
    public void Grep_ShowsEmptyResultsAndErrors(string result)
    {
        ToolResultText.Summarize("Grep", JsonSerializer.SerializeToElement(result)).Should().Equal(result);
    }

    [Fact]
    public void OtherTools_ShowNothing()
    {
        ToolResultText.Summarize("Read", "     1→hello").Should().BeEmpty();
    }

    [Fact]
    public void JsonElementResults_AreHandled()
    {
        var json = JsonSerializer.SerializeToElement(UnittestOk);

        ToolResultText.Summarize("PowerShell", json, maxLines: 1).Should().Equal("exit 0", "OK");
    }
}
