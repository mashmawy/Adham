using Adham.Tools.Abstractions;
using Adham.Tools.Files;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Files.Tests;

public sealed class ReadToolTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("adham-read-").FullName;
    private readonly ReadTool _tool;

    public ReadToolTests() => _tool = new ReadTool(new WorkingDirectory(_dir));

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

    [Fact]
    public async Task ReadsRelativePath_WithLineNumbers()
    {
        Write("a.txt", "first\nsecond");

        var result = await _tool.ExecuteAsync("a.txt");

        result.Should().Be("     1→first\n     2→second");
    }

    [Fact]
    public async Task OffsetAndLimit_ReturnASlice_AndSayThereIsMore()
    {
        Write("a.txt", "1\n2\n3\n4\n5");

        var result = await _tool.ExecuteAsync("a.txt", offset: 2, limit: 2);

        result.Should().Be("     2→2\n     3→3\n<showing lines 2-3 of 5; use offset to read more>");
    }

    [Fact]
    public async Task MissingFile_ReturnsAnErrorTheModelCanActOn()
    {
        var result = await _tool.ExecuteAsync("nope.txt");

        result.Should().StartWith("Error: file not found: nope.txt");
    }

    [Fact]
    public async Task Folder_PointsTheModelToGlob()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "src"));

        var result = await _tool.ExecuteAsync("src");

        result.Should().Contain("Use Glob");
    }

    [Fact]
    public async Task BinaryFile_ReturnsAPlaceholder()
    {
        File.WriteAllBytes(Path.Combine(_dir, "x.bin"), [1, 0, 2, 0, 3]);

        var result = await _tool.ExecuteAsync("x.bin");

        result.Should().Be("<binary file: not shown>");
    }

    [Fact]
    public async Task EmptyFile_SaysSo()
    {
        Write("empty.txt", "");

        var result = await _tool.ExecuteAsync("empty.txt");

        result.Should().Be("<file is empty>");
    }

    [Fact]
    public void Schema_UsesSnakeCaseParameterNames()
    {
        var schema = _tool.AsAIFunction().JsonSchema.ToString();

        schema.Should().Contain("file_path").And.Contain("offset").And.Contain("limit");
    }
}
