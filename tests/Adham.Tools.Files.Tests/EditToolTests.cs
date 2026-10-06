using Adham.Tools.Abstractions;
using Adham.Tools.Files;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Files.Tests;

public sealed class EditToolTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("adham-edit-").FullName;
    private readonly FileReadTracker _reads = new();
    private readonly ReadTool _read;
    private readonly EditTool _edit;
    private readonly WriteTool _write;
    private readonly ScriptedApprover _approver = new();

    public EditToolTests()
    {
        var cwd = new WorkingDirectory(_dir);
        _read = new ReadTool(cwd, _reads);
        _edit = new EditTool(cwd, _reads, _approver);
        _write = new WriteTool(cwd, _reads, _approver);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string PathOf(string name) => Path.Combine(_dir, name);

    private async Task Given(string name, string content)
    {
        File.WriteAllText(PathOf(name), content);
        await _read.ExecuteAsync(name);
    }

    [Fact]
    public async Task UniqueMatch_IsReplaced()
    {
        await Given("Calc.cs", "int Add(int a, int b) => a - b;\n");

        var result = await _edit.ExecuteAsync("Calc.cs", "a - b", "a + b");

        result.Should().Contain("updated successfully");
        File.ReadAllText(PathOf("Calc.cs")).Should().Be("int Add(int a, int b) => a + b;\n");
    }

    [Fact]
    public async Task SeveralMatches_WithoutReplaceAll_AreRefused()
    {
        await Given("a.txt", "x = 1\nx = 1\n");

        var result = await _edit.ExecuteAsync("a.txt", "x = 1", "x = 2");

        result.Should().Contain("found 2 matches").And.Contain("replace_all");
        File.ReadAllText(PathOf("a.txt")).Should().Be("x = 1\nx = 1\n");
    }

    [Fact]
    public async Task ReplaceAll_ReplacesEveryMatch()
    {
        await Given("a.txt", "x = 1\nx = 1\n");

        await _edit.ExecuteAsync("a.txt", "x = 1", "x = 2", replace_all: true);

        File.ReadAllText(PathOf("a.txt")).Should().Be("x = 2\nx = 2\n");
    }

    [Fact]
    public async Task StaleOldString_ShowsTheClosestLine_AndHowToReRead()
    {
        await Given("a.cs", "class A\n{\n    int Count = 2;\n}\n");

        var result = await _edit.ExecuteAsync("a.cs", "    int Count = 1;", "    int Count = 3;");

        result.Should().Contain("not found")
            .And.Contain(">>>     3      int Count = 2;")
            .And.Contain("\"offset\":1,\"limit\":5");
    }

    [Fact]
    public async Task InventedOldString_SuggestsAFullReRead()
    {
        await Given("a.cs", "class A {}\n");

        var result = await _edit.ExecuteAsync("a.cs", "nothing like this", "x");

        result.Should().Contain("No similar line found").And.Contain("Read the whole file again");
    }

    [Fact]
    public async Task FileNeverRead_IsRefused()
    {
        File.WriteAllText(PathOf("a.txt"), "hello");

        var result = await _edit.ExecuteAsync("a.txt", "hello", "bye");

        result.Should().Contain("has not been read this session").And.Contain("Recovery: call Read");
        File.ReadAllText(PathOf("a.txt")).Should().Be("hello");
    }

    [Fact]
    public async Task FileChangedAfterTheRead_IsRefused()
    {
        await Given("a.txt", "hello");
        File.WriteAllText(PathOf("a.txt"), "hello, changed elsewhere");

        var result = await _edit.ExecuteAsync("a.txt", "hello", "bye");

        result.Should().Contain("modified since it was last read");
    }

    [Fact]
    public async Task PartialRead_IsEnoughForEdit()
    {
        File.WriteAllText(PathOf("a.txt"), "1\n2\n3\n");
        await _read.ExecuteAsync("a.txt", offset: 2, limit: 1);

        var result = await _edit.ExecuteAsync("a.txt", "2", "two");

        result.Should().Contain("updated successfully");
    }

    [Fact]
    public async Task EditAfterAPartialRead_DoesNotUnlockWrite()
    {
        File.WriteAllText(PathOf("a.txt"), "1\n2\n3\n");
        await _read.ExecuteAsync("a.txt", offset: 2, limit: 1);
        await _edit.ExecuteAsync("a.txt", "2", "two");

        var result = await _write.ExecuteAsync("a.txt", "replaced");

        result.Should().Contain("has not been fully read");
    }

    [Fact]
    public async Task TwoEditsInARow_DoNotTripTheChangedCheck()
    {
        await Given("a.txt", "a b c");

        await _edit.ExecuteAsync("a.txt", "a", "A");
        var second = await _edit.ExecuteAsync("a.txt", "c", "C");

        second.Should().Contain("updated successfully");
        File.ReadAllText(PathOf("a.txt")).Should().Be("A b C");
    }

    [Fact]
    public async Task CrlfFile_KeepsItsLineEndings()
    {
        await Given("win.cs", "line one\r\nline two\r\n");

        await _edit.ExecuteAsync("win.cs", "line one\nline two", "line 1\nline 2");

        File.ReadAllText(PathOf("win.cs")).Should().Be("line 1\r\nline 2\r\n");
    }

    [Fact]
    public async Task SameOldAndNew_IsRefused()
    {
        await Given("a.txt", "x");

        (await _edit.ExecuteAsync("a.txt", "x", "x")).Should().Contain("no changes to make");
    }

    [Fact]
    public async Task MissingFile_PointsToWrite()
    {
        (await _edit.ExecuteAsync("nope.txt", "a", "b")).Should().Contain("use Write");
    }

    [Fact]
    public void Schema_UsesSnakeCase_AndReplaceAllIsOptional()
    {
        var schema = _edit.AsAIFunction().JsonSchema.ToString();

        schema.Should().Contain("old_string").And.Contain("new_string").And.Contain("replace_all");
        schema.Should().NotContain("\"replace_all\"]").And.Contain("\"required\":[\"file_path\",\"old_string\",\"new_string\"]");
    }

    [Fact]
    public async Task UserIsAsked_WithTheWholeFileBeforeAndAfter()
    {
        await Given("Calc.cs", "int Add(int a, int b) => a - b;\n");

        await _edit.ExecuteAsync("Calc.cs", "a - b", "a + b");

        _approver.Asked.Should().ContainSingle().Which.Should().Be(new FileChange(
            "Calc.cs", "int Add(int a, int b) => a - b;\n", "int Add(int a, int b) => a + b;\n"));
    }

    [Fact]
    public async Task Declined_LeavesTheFileUntouched()
    {
        await Given("a.txt", "hello");
        _approver.Answer = false;

        var result = await _edit.ExecuteAsync("a.txt", "hello", "bye");

        result.Should().Contain("declined");
        File.ReadAllText(PathOf("a.txt")).Should().Be("hello");
    }

    [Fact]
    public async Task RefusedEdits_NeverReachTheUser()
    {
        File.WriteAllText(PathOf("a.txt"), "hello");

        await _edit.ExecuteAsync("a.txt", "hello", "bye");

        _approver.Asked.Should().BeEmpty();
    }
}
