using Adham.Tools.Abstractions;
using Adham.Tools.Files;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Files.Tests;

// Seen live: the model wrote "[310 lines, removed to save context]" (a note Adham sends in place of old
// tool output) as a document's whole content, and a 310-line file became one line.
public sealed class ContextNotesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("adham-notes-").FullName;
    private readonly FileReadTracker _reads = new();
    private readonly ScriptedApprover _approver = new();
    private readonly WriteTool _write;
    private readonly EditTool _edit;
    private readonly ReadTool _read;

    public ContextNotesTests()
    {
        var cwd = new WorkingDirectory(_dir);
        _read = new ReadTool(cwd, _reads);
        _write = new WriteTool(cwd, _reads, _approver);
        _edit = new EditTool(cwd, _reads, _approver);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    // The notes Adham.Core sends (OldToolResults): an argument note and a result note.
    public static TheoryData<string> Notes => new()
    {
        "[310 lines, removed to save context]",
        "[Earlier result of Read(file_path: docs/design.md): 186 lines, removed to save context. Call Read again if you need it.]",
        "# Design\n\n[40 lines, removed to save context]\n",
    };

    [Theory]
    [MemberData(nameof(Notes))]
    public async Task Write_RefusesANoteAsContent_AndLeavesTheFile(string content)
    {
        var path = Path.Combine(_dir, "doc.md");
        File.WriteAllText(path, "the real document");
        await _read.ExecuteAsync("doc.md");

        var result = await _write.ExecuteAsync("doc.md", content);

        result.Should().StartWith("Error: content contains").And.Contain("Read the file");
        File.ReadAllText(path).Should().Be("the real document");
    }

    [Fact]
    public async Task Edit_RefusesANoteAsNewText()
    {
        var path = Path.Combine(_dir, "doc.md");
        File.WriteAllText(path, "intro\nbody\n");
        await _read.ExecuteAsync("doc.md");

        var result = await _edit.ExecuteAsync("doc.md", "body", "[12 lines, removed to save context]");

        result.Should().StartWith("Error: new_string contains");
        File.ReadAllText(path).Should().Be("intro\nbody\n");
    }

    [Fact]
    public async Task ThePhraseInsideALine_IsFine()
    {
        // e.g. documentation about this very feature.
        var result = await _write.ExecuteAsync("notes.md", "Old output becomes `[N lines, removed to save context]`.\n");

        result.Should().StartWith("File created successfully");
    }
}
