using Adham.Core;
using FluentAssertions;
using Xunit;

namespace Adham.Core.Tests;

public class SystemPromptTests
{
    private static readonly string Prompt = SystemPrompt.Build(@"C:\code\shop", "PowerShell");

    [Fact]
    public void NamesTheWorkingDirectoryAndTheShellTool()
    {
        Prompt.Should().Contain(@"Working directory: C:\code\shop").And.Contain("with the PowerShell tool");
    }

    [Fact]
    public void AsksForReadingBeforeChanging_AndTestsAfter()
    {
        Prompt.Should().Contain("Read it first").And.Contain("run the tests to check your fix");
    }

    [Fact]
    public void AsksToLearnTheProjectBeforeGuessing()
    {
        // Seen live: C# searches in a Python project, and "pytest" tried in every run although the README
        // says "python -m unittest" (and pytest isn't installed).
        Prompt.Should().Contain("Glob **/*").And.Contain("README")
            .And.Contain("test command the project documents")
            .And.Contain("Don't assume a tool is installed");
    }

    [Fact]
    public void SaysCommandsAlreadyRunInTheWorkingDirectory()
    {
        Prompt.Should().Contain("don't cd into it");
    }
}
