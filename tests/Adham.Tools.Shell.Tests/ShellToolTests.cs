using Adham.Tools.Abstractions;
using Adham.Tools.Shell;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Shell.Tests;

public sealed class ShellToolTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("adham-shelltool-").FullName;
    private readonly Shell _shell = Shell.ForThisMachine();
    private readonly ScriptedCommandApprover _approver = new();
    private readonly ShellTool _tool;

    public ShellToolTests() => _tool = new ShellTool(_shell, new WorkingDirectory(_dir), _approver);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private bool IsPs => _shell.Kind == ShellKind.PowerShell;
    private string MakeFile(string name) => IsPs ? $"New-Item -ItemType File {name}" : $"touch {name}";

    [Fact]
    public void Name_FollowsTheShell()
    {
        _tool.Name.Should().Be(IsPs ? "PowerShell" : "Bash");
    }

    [Fact]
    public async Task ReadOnlyCommand_RunsWithoutAsking()
    {
        File.WriteAllText(Path.Combine(_dir, "hello.txt"), "x");

        var result = await _tool.ExecuteAsync(IsPs ? "Get-ChildItem -Name" : "ls");

        _approver.Asked.Should().BeEmpty();
        result.Should().Contain("exit_code=0").And.Contain("hello.txt");
    }

    [Fact]
    public async Task OtherCommand_AsksFirst_AndRunsWhenApproved()
    {
        var result = await _tool.ExecuteAsync(MakeFile("made.txt"));

        _approver.Asked.Should().ContainSingle().Which.Should().Be(new CommandRequest(_tool.Name, MakeFile("made.txt")));
        result.Should().Contain("exit_code=0");
        File.Exists(Path.Combine(_dir, "made.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task DeclinedCommand_DoesNotRun()
    {
        _approver.Answer = false;

        var result = await _tool.ExecuteAsync(MakeFile("made.txt"));

        result.Should().Be(ShellTool.DeclinedMessage);
        File.Exists(Path.Combine(_dir, "made.txt")).Should().BeFalse();
    }

    [Fact]
    public async Task DestructiveCommand_IsRefused_WithoutAsking()
    {
        // Harmless here even if it ran (no repo, no remote), but it must never get that far.
        var result = await _tool.ExecuteAsync("git push --force");

        result.Should().StartWith("Refused: git push --force").And.Contain("can't be approved");
        _approver.Asked.Should().BeEmpty();
    }

    [Fact]
    public async Task FailingCommand_ReturnsTheExitCodeAndTheRetryNote()
    {
        _approver.Answer = true;

        var result = await _tool.ExecuteAsync("exit 4"); // same syntax in both shells

        result.Should().Contain("exit_code=4").And.Contain("Fix the command and retry");
    }

    [Fact]
    public void Description_MatchesTheShell()
    {
        _tool.Description.Should().Contain(_dir).And.Contain("64 KB").And.Contain("refused");
        if (IsPs)
            _tool.Description.Should().Contain("PowerShell");
    }

    [Fact]
    public void Schema_HasCommandAndOptionalTimeout()
    {
        var schema = _tool.AsAIFunction().JsonSchema.ToString();

        schema.Should().Contain("\"command\"").And.Contain("\"timeout\"").And.Contain("\"required\":[\"command\"]");
    }

    private sealed class ScriptedCommandApprover : ICommandApprover
    {
        public bool Answer { get; set; } = true;
        public List<CommandRequest> Asked { get; } = [];

        public ValueTask<bool> ApproveAsync(CommandRequest request, CancellationToken cancellationToken)
        {
            Asked.Add(request);
            return ValueTask.FromResult(Answer);
        }
    }
}
