using Adham.Tools.Shell;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Shell.Tests;

public class CommandSplitterTests
{
    private static SplitCommand Bash(string c) => CommandSplitter.Split(c, ShellKind.Bash);
    private static SplitCommand Ps(string c) => CommandSplitter.Split(c, ShellKind.PowerShell);

    [Fact]
    public void SimpleCommand_IsOneSegment_WithWords()
    {
        var split = Bash("git log --oneline -5");

        split.IsRisky.Should().BeFalse();
        split.Segments.Should().ContainSingle().Which.Words.Should().Equal("git", "log", "--oneline", "-5");
    }

    [Theory]
    [InlineData("ls && rm -rf ~")]
    [InlineData("ls || rm -rf ~")]
    [InlineData("ls; rm -rf ~")]
    [InlineData("ls | rm -rf ~")]
    public void Separators_SplitIntoSegments(string command)
    {
        // The whole point: a safe-looking first word must not vouch for the rest of the line.
        Bash(command).Segments.Select(s => s.Name).Should().Equal("ls", "rm");
        Ps(command).Segments.Select(s => s.Name).Should().Equal("ls", "rm");
    }

    [Fact]
    public void SeparatorsInsideQuotes_DoNotSplit()
    {
        var split = Bash("git commit -m 'fix: a && b; c | d'");

        split.Segments.Should().ContainSingle().Which.Words[^1].Should().Be("fix: a && b; c | d");
        split.IsRisky.Should().BeFalse();
    }

    [Theory]
    [InlineData("ls $(rm -rf ~)", "substitution")]
    [InlineData("ls `rm -rf ~`", "backticks")]
    [InlineData("cat $HOME/.ssh/id_rsa", "variable")]
    [InlineData("echo \"$(whoami)\"", "inside quotes")]
    [InlineData("ls > out.txt", "redirect")]
    [InlineData("cat < secrets", "redirect")]
    [InlineData("sleep 100 & rm x", "background")]
    [InlineData("ls\nrm -rf ~", "newline")]
    [InlineData("(cd / && rm x)", "subshell")]
    [InlineData("ls \\; rm", "escape")]
    public void Bash_RiskyConstructs_AreFlagged(string command, string reasonContains)
    {
        Bash(command).Risky.Should().Contain(reasonContains);
    }

    [Theory]
    [InlineData("Get-ChildItem $env:USERPROFILE", "variable")]
    [InlineData("Remove-Item (Get-Item x)", "parenthesized")]
    [InlineData("Get-ChildItem | Where-Object { $_.Length -gt 0 }", "script block")]
    [InlineData("[System.IO.File]::Delete('x')", "static call")]
    [InlineData("Get-ChildItem @args", "splatting")]
    [InlineData("& 'C:\\tools\\x.exe'", "call operator")]
    [InlineData("Get-Content x > y.txt", "redirect")]
    [InlineData("cmd --% /c del x", "stop-parsing")]
    [InlineData("Write-Output `$x", "escape")]
    public void PowerShell_RiskyConstructs_AreFlagged(string command, string reasonContains)
    {
        Ps(command).Risky.Should().Contain(reasonContains);
    }

    [Theory]
    [InlineData("python -m unittest 2>&1")]
    [InlineData("git status 2>&1")]
    public void MergingStderrIntoStdout_IsNotRisky(string command)
    {
        Bash(command).IsRisky.Should().BeFalse();
        Ps(command).IsRisky.Should().BeFalse();
    }

    [Fact]
    public void PowerShell_BackslashPaths_AreNotEscapes()
    {
        var split = Ps("Get-Content 'C:\\code\\app.cs'; Get-ChildItem src\\tests");

        split.IsRisky.Should().BeFalse();
        split.Segments[1].Words.Should().Equal("Get-ChildItem", "src\\tests");
    }

    [Fact]
    public void SingleQuotes_AreLiteral_EvenWithDollar()
    {
        Bash("grep '$HOME' notes.txt").IsRisky.Should().BeFalse();
        Ps("Select-String -Pattern '$x' notes.txt").IsRisky.Should().BeFalse();
    }

    [Fact]
    public void UnclosedQuote_IsRisky()
    {
        Bash("echo 'oops").IsRisky.Should().BeTrue();
    }
}
