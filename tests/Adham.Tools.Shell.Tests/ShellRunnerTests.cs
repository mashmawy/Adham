using Adham.Tools.Shell;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Shell.Tests;

// Real processes in this machine's shell: PowerShell on Windows, bash elsewhere (CI runs both).
public sealed class ShellRunnerTests : IDisposable
{
    private readonly Shell _shell = Shell.ForThisMachine();
    private readonly string _dir = Directory.CreateTempSubdirectory("adham-shell-").FullName;
    private bool IsPs => _shell.Kind == ShellKind.PowerShell;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private Task<ShellResult> Run(string command, int timeoutMs = 30_000) =>
        ShellRunner.RunAsync(_shell, command, _dir, timeoutMs, default);

    [Fact]
    public async Task Output_AndExitCodeZero()
    {
        var result = await Run(IsPs ? "Write-Output 'hello'" : "echo hello");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Be("hello\n");
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task NonZeroExitCode_AndStderr_AreReturned_NotThrown()
    {
        var result = await Run(IsPs ? "[Console]::Error.WriteLine('oops'); exit 3" : "echo oops >&2; exit 3");

        result.ExitCode.Should().Be(3);
        result.Stderr.Should().Contain("oops");
    }

    [Fact]
    public async Task RunsInTheWorkingDirectory()
    {
        // A marker file instead of comparing paths: on macOS the temp folder sits behind a symlink.
        File.WriteAllText(Path.Combine(_dir, "marker-7f3a.txt"), "x");

        var result = await Run(IsPs ? "Get-ChildItem -Name" : "ls");

        result.Stdout.Should().Contain("marker-7f3a.txt");
    }

    [Fact]
    public async Task Timeout_StopsTheProcess_AndKeepsEarlyOutput()
    {
        var result = await Run(IsPs ? "Write-Output 'started'; Start-Sleep -Seconds 30" : "echo started; sleep 30", timeoutMs: 3000);

        result.TimedOut.Should().BeTrue();
        result.ExitCode.Should().Be(-1);
        result.Stdout.Should().Contain("started");
    }

    [Fact]
    public async Task HugeOutput_IsCapped()
    {
        var result = await Run(IsPs ? "1..20000 | ForEach-Object { \"line $_\" }" : "seq 1 20000 | sed 's/^/line /'");

        result.StdoutTruncated.Should().BeTrue();
        System.Text.Encoding.UTF8.GetByteCount(result.Stdout).Should().BeLessThanOrEqualTo(ShellRunner.MaxStreamBytes);
        result.Stdout.Should().StartWith("line 1\n");
    }

    [Fact]
    public async Task NonAsciiOutput_SurvivesTheRoundTrip()
    {
        var result = await Run(IsPs ? "Write-Output 'مرحبا ⚙'" : "echo 'مرحبا ⚙'");

        result.Stdout.Should().Be("مرحبا ⚙\n");
    }

    [Theory]
    [InlineData(null, ShellRunner.DefaultTimeoutMs)]
    [InlineData(0, ShellRunner.DefaultTimeoutMs)]
    [InlineData(5000, 5000)]
    [InlineData(9_999_999, ShellRunner.MaxTimeoutMs)]
    public void Timeout_IsClampedToTheMaximum(int? requested, int expected)
    {
        ShellRunner.ClampTimeout(requested).Should().Be(expected);
    }
}
