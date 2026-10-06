using Adham.Tools.Abstractions;
using Adham.Tools.Files;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Files.Tests;

public sealed class FileReadTrackerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("adham-reads-").FullName;
    private readonly FileReadTracker _reads = new();
    private readonly ReadTool _read;

    public FileReadTrackerTests() => _read = new ReadTool(new WorkingDirectory(_dir), _reads);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task FullRead_IsKnownAndFull()
    {
        var path = Write("a.txt", "1\n2\n3");

        await _read.ExecuteAsync("a.txt");

        _reads.IsKnown(path).Should().BeTrue();
        _reads.WasFullRead(path).Should().BeTrue();
        _reads.HasChangedSinceRead(path).Should().BeFalse();
    }

    [Fact]
    public async Task OffsetOrLimitRead_IsKnownButNotFull()
    {
        var path = Write("a.txt", "1\n2\n3");

        await _read.ExecuteAsync("a.txt", offset: 2, limit: 1);

        _reads.IsKnown(path).Should().BeTrue();
        _reads.WasFullRead(path).Should().BeFalse();
    }

    [Fact]
    public async Task PartialReadAfterAFullRead_DoesNotDowngradeIt()
    {
        var path = Write("a.txt", "1\n2\n3");

        await _read.ExecuteAsync("a.txt");
        await _read.ExecuteAsync("a.txt", offset: 2, limit: 1);

        _reads.WasFullRead(path).Should().BeTrue();
    }

    [Fact]
    public async Task ChangeOnDisk_IsDetected()
    {
        var path = Write("a.txt", "short");
        await _read.ExecuteAsync("a.txt");

        File.WriteAllText(path, "something much longer");

        _reads.HasChangedSinceRead(path).Should().BeTrue();
    }

    [Fact]
    public void UnreadFile_IsUnknown_AndCountsAsChanged()
    {
        var path = Write("a.txt", "x");

        _reads.IsKnown(path).Should().BeFalse();
        _reads.HasChangedSinceRead(path).Should().BeTrue();
    }

    [Fact]
    public async Task PathsAreCaseInsensitive()
    {
        var path = Write("Program.cs", "x");
        await _read.ExecuteAsync("Program.cs");

        _reads.IsKnown(path.ToUpperInvariant()).Should().BeTrue();
    }
}
