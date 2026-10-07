using Adham.Tools.Shell;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Shell.Tests;

public class ReadOnlyRulesTests
{
    private static bool Bash(string c) => ReadOnlyRules.IsReadOnly(CommandSplitter.Split(c, ShellKind.Bash), ShellKind.Bash);
    private static bool Ps(string c) => ReadOnlyRules.IsReadOnly(CommandSplitter.Split(c, ShellKind.PowerShell), ShellKind.PowerShell);

    [Theory]
    [InlineData("ls -la src")]
    [InlineData("cat README.md")]
    [InlineData("head -n 20 app.py | wc -l")]
    [InlineData("grep -rn TODO src")]
    [InlineData("rg -n 'def main'")]
    [InlineData("find . -name '*.py'")]
    [InlineData("git status")]
    [InlineData("git diff --stat")]
    [InlineData("git log --oneline -10")]
    [InlineData("git branch -a")]
    [InlineData("git tag")]
    [InlineData("git remote -v")]
    [InlineData("git stash list")]
    [InlineData("git status && git diff")]
    [InlineData("ls 2>&1")]
    public void Bash_ReadOnlyCommands_AreAllowed(string command)
    {
        Bash(command).Should().BeTrue();
    }

    [Theory]
    [InlineData("ls && rm notes.txt")]           // one segment isn't read-only
    [InlineData("ls $(rm notes.txt)")]            // risky construct
    [InlineData("cat file > copy.txt")]           // redirect writes
    [InlineData("find . -name '*.tmp' -delete")]
    [InlineData("find . -exec rm {} ;")]
    [InlineData("rg --pre ./script.sh x")]
    [InlineData("sort -o out.txt in.txt")]
    [InlineData("uniq in.txt out.txt")]
    [InlineData("git branch new-feature")]
    [InlineData("git branch -D old")]
    [InlineData("git tag v1.0")]
    [InlineData("git remote add origin x")]
    [InlineData("git stash")]
    [InlineData("git -c core.pager=evil log")]
    [InlineData("git diff --output=patch.txt")]
    [InlineData("git commit -m x")]
    [InlineData("python -m unittest")]            // runs project code: asks
    [InlineData("pytest")]
    [InlineData("cat .env")]                      // secrets always ask
    [InlineData("cat ~/.ssh/id_rsa")]
    [InlineData("grep KEY config/.env")]
    public void Bash_EverythingElse_IsNotAutoAllowed(string command)
    {
        Bash(command).Should().BeFalse();
    }

    [Theory]
    [InlineData("Get-ChildItem -Recurse -Filter *.cs")]
    [InlineData("ls")]
    [InlineData("dir src")]
    [InlineData("Get-Content README.md")]
    [InlineData("cat invoice\\pricing.py")]
    [InlineData("Select-String -Path *.py -Pattern 'TODO'")]
    [InlineData("Get-ChildItem | Measure-Object")]
    [InlineData("Test-Path main.py")]
    [InlineData("git log --oneline -5")]
    [InlineData("git status; git diff")]
    public void PowerShell_ReadOnlyCommands_AreAllowed(string command)
    {
        Ps(command).Should().BeTrue();
    }

    [Theory]
    [InlineData("Remove-Item notes.txt")]
    [InlineData("Get-ChildItem | Remove-Item")]
    [InlineData("Set-Content a.txt 'x'")]
    [InlineData("Get-Content a.txt > b.txt")]
    [InlineData("Get-ChildItem $env:USERPROFILE")]
    [InlineData("Get-ChildItem | Where-Object { $_.Length -gt 0 }")]
    [InlineData("python -m unittest")]
    [InlineData("dotnet test")]
    [InlineData("Get-Content .env")]
    [InlineData("type C:\\Users\\me\\.ssh\\id_rsa")]
    [InlineData("git push")]
    public void PowerShell_EverythingElse_IsNotAutoAllowed(string command)
    {
        Ps(command).Should().BeFalse();
    }

    [Fact]
    public void EnvironmentLikeNames_ThatAreNotSecrets_AreFine()
    {
        Bash("cat environment.md").Should().BeTrue();
        Bash("ls src/.envoy-config").Should().BeTrue();
    }
}
