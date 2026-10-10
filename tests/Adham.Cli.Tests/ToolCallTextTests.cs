using System.Text.Json;
using Adham.Cli;
using Adham.Tools.Abstractions;
using FluentAssertions;
using Xunit;

namespace Adham.Cli.Tests;

public class ToolCallTextTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "adham-demo");
    private static readonly WorkingDirectory Cwd = new(Root);

    [Fact]
    public void AbsolutePathInsideTheWorkingDirectory_IsShownRelative()
    {
        // Seen live: "⚙ Read(file_path: C:\Users\...\adham-demo-invoice\invoice\pricing.py)".
        var args = new Dictionary<string, object?> { ["file_path"] = Path.Combine(Root, "invoice", "pricing.py") };

        ToolCallText.Format("Read", args, Cwd).Should().Be("Read(file_path: invoice/pricing.py)");
    }

    [Fact]
    public void ArgumentsAsJsonElements_AreHandled()
    {
        // The OpenAI client hands arguments over as JsonElement values.
        var json = JsonSerializer.SerializeToElement(Path.Combine(Root, "README.md"));

        ToolCallText.Format("Read", new Dictionary<string, object?> { ["file_path"] = json }, Cwd)
            .Should().Be("Read(file_path: README.md)");
    }

    [Fact]
    public void RelativePaths_AndOtherArguments_AreUnchanged()
    {
        var args = new Dictionary<string, object?> { ["pattern"] = "**/*.cs", ["path"] = "src", ["limit"] = 5 };

        ToolCallText.Format("Glob", args, Cwd).Should().Be("Glob(pattern: **/*.cs, path: src, limit: 5)");
    }

    [Fact]
    public void AbsolutePathOutsideTheWorkingDirectory_IsKept()
    {
        var outside = Path.Combine(Path.GetTempPath(), "somewhere-else", "notes.txt");

        ToolCallText.Format("Read", new Dictionary<string, object?> { ["file_path"] = outside }, Cwd)
            .Should().Be($"Read(file_path: {outside})");
    }

    [Fact]
    public void NoArguments_ShowsEmptyParentheses()
    {
        ToolCallText.Format("Glob", null, Cwd).Should().Be("Glob()");
    }

    [Fact]
    public void AskUser_HidesTheQuestion_WhichThePromptShowsAnyway()
    {
        var args = new Dictionary<string, object?> { ["question"] = "What is your name?" };

        ToolCallText.Format("AskUser", args, Cwd).Should().Be("AskUser()");
    }
}
