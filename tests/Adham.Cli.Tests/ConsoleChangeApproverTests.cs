using Adham.Cli;
using Adham.Tools.Abstractions;
using FluentAssertions;
using Xunit;

namespace Adham.Cli.Tests;

public class ConsoleChangeApproverTests
{
    private static async Task<(bool Approved, string Shown)> Ask(string typed, FileChange change)
    {
        using var output = new StringWriter();
        var approver = new ConsoleChangeApprover(new StringReader(typed), output, color: false);
        var approved = await approver.ApproveAsync(change, default);
        return (approved, output.ToString());
    }

    private static readonly FileChange Fix = new("src/Calc.cs", "return a - b;\n", "return a + b;\n");

    [Theory]
    [InlineData("y\n")]
    [InlineData("Y\n")]
    [InlineData("yes\n")]
    public async Task Yes_Approves(string typed)
    {
        (await Ask(typed, Fix)).Approved.Should().BeTrue();
    }

    [Theory]
    [InlineData("n\n")]
    [InlineData("\n")]
    [InlineData("")]
    [InlineData("sure\n")]
    public async Task AnythingElse_Declines(string typed)
    {
        (await Ask(typed, Fix)).Approved.Should().BeFalse();
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
}
