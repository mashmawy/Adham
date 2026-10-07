using Adham.Tools.Shell;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Shell.Tests;

public class ShellResultTextTests
{
    private static readonly Shell Ps = new(ShellKind.PowerShell, "pwsh");
    private static readonly Shell Bash = new(ShellKind.Bash, "/bin/bash");

    [Fact]
    public void Success_ShowsCommandExitCodeAndStreams()
    {
        var text = ShellResultText.Format(Bash, "git status", new ShellResult(0, "clean\n", "", false, false, false));

        text.Should().Be("$ git status\nexit_code=0\nstdout:\nclean\nstderr:");
    }

    [Fact]
    public void Failure_AddsTheRetryOrReportNote()
    {
        var text = ShellResultText.Format(Ps, "dotnet test", new ShellResult(1, "", "1 failed\n", false, false, false));

        text.Should().StartWith("PS> dotnet test\nexit_code=1").And.EndWith(ShellResultText.FailureNote);
    }

    [Fact]
    public void Timeout_IsMarked()
    {
        var text = ShellResultText.Format(Bash, "sleep 99", new ShellResult(-1, "", "", true, false, false));

        text.Should().Contain("status=timeout").And.Contain(ShellResultText.FailureNote);
    }

    [Fact]
    public void TruncatedOutput_SaysSoAndHowToNarrowIt()
    {
        var text = ShellResultText.Format(Bash, "cat big.log", new ShellResult(0, "a\n", "", false, true, false));

        text.Should().Contain("[stdout cut at 64 KB: narrow the command");
    }
}
