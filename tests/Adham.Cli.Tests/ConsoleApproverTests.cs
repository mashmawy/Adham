using Adham.Cli;
using Adham.Tools.Abstractions;
using FluentAssertions;
using Xunit;

namespace Adham.Cli.Tests;

public class ConsoleApproverTests
{
    private static async Task<(bool Approved, string Shown)> Ask(string typed, FileChange change)
    {
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new StringReader(typed), output, color: false);
        var approved = await approver.ApproveAsync(change, default);
        return (approved, output.ToString());
    }

    private static async Task<(bool Approved, string Shown)> Ask(string typed, CommandRequest request)
    {
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new StringReader(typed), output, color: false);
        var approved = await approver.ApproveAsync(request, default);
        return (approved, output.ToString());
    }

    private static readonly FileChange Fix = new("src/Calc.cs", "return a - b;\n", "return a + b;\n");
    private static readonly CommandRequest Tests = new("PowerShell", "python -m unittest");

    [Theory]
    [InlineData("y\n")]
    [InlineData("Y\n")]
    [InlineData("yes\n")]
    public async Task Yes_Approves(string typed)
    {
        (await Ask(typed, Fix)).Approved.Should().BeTrue();
        (await Ask(typed, Tests)).Approved.Should().BeTrue();
    }

    [Theory]
    [InlineData("n\n")]
    [InlineData("\n")]
    [InlineData("")]
    [InlineData("sure\n")]
    public async Task AnythingElse_Declines(string typed)
    {
        (await Ask(typed, Fix)).Approved.Should().BeFalse();
        (await Ask(typed, Tests)).Approved.Should().BeFalse();
    }

    [Fact]
    public async Task ShowsTheDiff_AndThePrompt()
    {
        var (_, shown) = await Ask("n\n", Fix);

        shown.Should().Contain("Change to src/Calc.cs:")
            .And.Contain("- return a - b;")
            .And.Contain("+ return a + b;")
            .And.Contain("Allow this change to src/Calc.cs? [y/N]")
            .And.Contain("(declined)");
    }

    [Fact]
    public async Task NewFile_IsLabelledAsNew()
    {
        var (_, shown) = await Ask("y\n", new FileChange("notes.md", null, "# Notes\n"));

        shown.Should().Contain("New file: notes.md").And.Contain("+ # Notes");
    }

    [Fact]
    public async Task Command_ShowsTheShellAndTheExactCommand()
    {
        var (_, shown) = await Ask("y\n", Tests);

        shown.Should().Contain("Run in PowerShell:")
            .And.Contain("    python -m unittest")
            .And.Contain("Run this command? [y/N]")
            .And.Contain("(approved)");
    }

    private static async Task<string> AskUser(string typed, string question)
    {
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new StringReader(typed), output, color: false);
        return await approver.AskUserAsync(question, null, CancellationToken.None);
    }

    [Fact]
    public async Task AskUser_ReturnsTrimmedAnswer()
    {
        var answer = await AskUser("  hello world  \n", "What is it?");
        answer.Should().Be("hello world");
    }

    [Fact]
    public async Task AskUser_EmptyLine_ReturnsNoAnswerMessage()
    {
        var answer = await AskUser("\n", "What is it?");
        answer.Should().StartWith("The user gave no answer.").And.Contain("Don't guess it");
    }

    [Fact]
    public async Task AskUser_EOF_ReturnsNoAnswerMessage()
    {
        var answer = await AskUser("", "What is it?");
        answer.Should().StartWith("The user gave no answer (end of input).").And.Contain("Don't guess it");
    }

    [Fact]
    public async Task AskUser_ShowsPromptFormat()
    {
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new StringReader("y\n"), output, color: false);
        await approver.AskUserAsync("What is the project name?", null, CancellationToken.None);

        output.ToString().Should().Contain("\n  ? What is the project name?\n  > ");
    }

    [Fact]
    public async Task AskUser_WithOptionNumber_ReturnsSelectedOption()
    {
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new StringReader("2\n"), output, color: false);
        var options = new[] { "First", "Second", "Third" };

        var answer = await approver.AskUserAsync("Pick one", options, CancellationToken.None);

        answer.Should().Be("Second");
    }

    [Fact]
    public async Task AskUser_WithOptionTypingFullAnswer_ReturnsTypedAnswer()
    {
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new StringReader("Custom\n"), output, color: false);
        var options = new[] { "First", "Second" };

        var answer = await approver.AskUserAsync("Pick one", options, CancellationToken.None);

        answer.Should().Be("Custom");
    }

    [Fact]
    public async Task AskUser_OptionNumberOutOfRange_AsksAgain()
    {
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new StringReader("5\n2\n"), output, color: false);

        var answer = await approver.AskUserAsync("Pick one", ["First", "Second", "Third"], CancellationToken.None);

        answer.Should().Be("Second");
        output.ToString().Should().Contain("Pick 1-3, or type an answer.");
    }

    [Fact]
    public async Task AskUser_OptionNumber_ShowsThePickedOption()
    {
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new StringReader("2\n"), output, color: false);

        await approver.AskUserAsync("Which license?", ["MIT", "Apache-2.0"], CancellationToken.None);

        output.ToString().Should().EndWith("  (Apache-2.0)" + Environment.NewLine);
    }

    [Fact]
    public async Task AskUser_NumberWithoutOptions_IsTheAnswer()
    {
        var answer = await AskUser("42\n", "How many?");

        answer.Should().Be("42");
    }

    [Fact]
    public async Task AskUser_WithOptionNumberInOutput()
    {
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new StringReader("1\n"), output, color: false);
        var options = new[] { "Yes", "No" };

        await approver.AskUserAsync("Continue?", options, CancellationToken.None);

        output.ToString().Should().Contain("\n  ? Continue?\n    1. Yes\n    2. No\n  > ");
    }

    [Fact]
    public async Task AskUser_Cancelled_ThrowsWithoutAsking()
    {
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new StringReader("answer\n"), output, color: false);

        var ask = async () => await approver.AskUserAsync("What is it?", null, new CancellationToken(canceled: true));

        await ask.Should().ThrowAsync<OperationCanceledException>();
        output.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task AskUser_CancelledWhileWaiting_Throws()
    {
        using var cts = new CancellationTokenSource();
        using var output = new StringWriter();
        var approver = new ConsoleApprover(new CtrlCReader(cts), output, color: false);

        var ask = async () => await approver.AskUserAsync("What is it?", null, cts.Token);

        await ask.Should().ThrowAsync<OperationCanceledException>();
    }

    // Ctrl+C while ReadLine waits: the token is cancelled and ReadLine returns no line.
    private sealed class CtrlCReader(CancellationTokenSource cts) : TextReader
    {
        public override string? ReadLine()
        {
            cts.Cancel();
            return null;
        }
    }
}
