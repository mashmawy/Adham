using Adham.Tools.Abstractions;
using Adham.Tools.Files;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Files.Tests;

public sealed class WriteToolTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("adham-write-").FullName;
    private readonly FileReadTracker _reads = new();
    private readonly ReadTool _read;
    private readonly WriteTool _write;
    private readonly ScriptedApprover _approver = new();

    public WriteToolTests()
    {
        var cwd = new WorkingDirectory(_dir);
        _read = new ReadTool(cwd, _reads);
        _write = new WriteTool(cwd, _reads, _approver);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string PathOf(string name) => Path.Combine(_dir, name);

    [Fact]
    public async Task NewFile_IsCreated_IncludingMissingFolders()
    {
        var result = await _write.ExecuteAsync("src/New/Hello.cs", "class Hello {}");

        result.Should().StartWith("File created successfully");
        File.ReadAllText(PathOf("src/New/Hello.cs")).Should().Be("class Hello {}");
    }

    [Fact]
    public async Task ExistingFile_NeverRead_IsRefused_WithARecoveryHint()
    {
        File.WriteAllText(PathOf("a.txt"), "keep me");

        var result = await _write.ExecuteAsync("a.txt", "overwritten");

        result.Should().Contain("has not been fully read").And.Contain("Recovery: Read");
        File.ReadAllText(PathOf("a.txt")).Should().Be("keep me");
    }

    [Fact]
    public async Task ExistingFile_OnlyPartlyRead_IsRefused()
    {
        File.WriteAllText(PathOf("a.txt"), "1\n2\n3");
        await _read.ExecuteAsync("a.txt", offset: 1, limit: 1);

        var result = await _write.ExecuteAsync("a.txt", "overwritten");

        result.Should().Contain("has not been fully read");
    }

    [Fact]
    public async Task ExistingFile_FullyRead_IsOverwritten()
    {
        File.WriteAllText(PathOf("a.txt"), "old");
        await _read.ExecuteAsync("a.txt");

        var result = await _write.ExecuteAsync("a.txt", "new");

        result.Should().Contain("updated successfully");
        File.ReadAllText(PathOf("a.txt")).Should().Be("new");
    }

    [Fact]
    public async Task ExistingFile_ChangedAfterTheRead_IsRefused()
    {
        File.WriteAllText(PathOf("a.txt"), "old");
        await _read.ExecuteAsync("a.txt");
        File.WriteAllText(PathOf("a.txt"), "someone else changed this");

        var result = await _write.ExecuteAsync("a.txt", "new");

        result.Should().Contain("modified since it was last read");
        File.ReadAllText(PathOf("a.txt")).Should().Be("someone else changed this");
    }

    [Fact]
    public async Task WritingTheSameFileTwice_IsAllowed()
    {
        await _write.ExecuteAsync("a.txt", "first");

        var result = await _write.ExecuteAsync("a.txt", "second");

        result.Should().Contain("updated successfully");
        File.ReadAllText(PathOf("a.txt")).Should().Be("second");
    }

    [Fact]
    public void IsNotReadOnly()
    {
        _write.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public async Task UserIsAsked_WithTheBeforeAndAfter()
    {
        File.WriteAllText(PathOf("a.txt"), "old");
        await _read.ExecuteAsync("a.txt");

        await _write.ExecuteAsync("a.txt", "new");

        _approver.Asked.Should().ContainSingle()
            .Which.Should().Be(new FileChange("a.txt", "old", "new"));
    }

    [Fact]
    public async Task NewFile_IsAskedWithNoBefore()
    {
        await _write.ExecuteAsync("b.txt", "hello");

        _approver.Asked.Single().Before.Should().BeNull();
    }

    [Fact]
    public async Task Declined_WritesNothing_AndTellsTheModelToAsk()
    {
        _approver.Answer = false;

        var result = await _write.ExecuteAsync("b.txt", "hello");

        result.Should().Contain("declined").And.Contain("ask the user");
        File.Exists(PathOf("b.txt")).Should().BeFalse();
    }

    [Fact]
    public async Task ApprovalPrompt_ShowsThePathRelativeToTheWorkingDirectory()
    {
        await _write.ExecuteAsync(PathOf("docs/notes.md"), "# Notes");

        _approver.Asked.Single().Path.Should().Be("docs/notes.md");
    }
}
