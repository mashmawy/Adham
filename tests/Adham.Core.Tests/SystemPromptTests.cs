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
}
