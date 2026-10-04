using Adham.Tools.Abstractions;
using Adham.Tools.Search;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Search.Tests;

public sealed class GlobToolTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("adham-glob-").FullName;
    private readonly GlobTool _tool;

    public GlobToolTests() => _tool = new GlobTool(new WorkingDirectory(_dir));

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Touch(string relative)
    {
        var full = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "x");
    }

    [Fact]
    public void DoubleStar_FindsFilesInSubfolders_AsRelativePaths()
    {
        Touch("Program.cs");
        Touch("src/App/Service.cs");
        Touch("README.md");

        var result = _tool.Execute("**/*.cs");

        result.Split('\n').Should().BeEquivalentTo("Program.cs", "src/App/Service.cs");
    }

    [Fact]
    public void SingleStar_OnlyMatchesTheTopFolder()
    {
        Touch("Program.cs");
        Touch("src/Service.cs");

        _tool.Execute("*.cs").Should().Be("Program.cs");
    }

    [Fact]
    public void SkipsBuildOutputAndGit()
    {
        Touch("src/A.cs");
        Touch("src/bin/Debug/A.cs");
        Touch("src/obj/A.cs");
        Touch(".git/config.cs");

        _tool.Execute("**/*.cs").Should().Be("src/A.cs");
    }

    [Fact]
    public void PathArgument_LimitsTheSearch()
    {
        Touch("a/One.cs");
        Touch("b/Two.cs");

        _tool.Execute("**/*.cs", path: "b").Should().Be("b/Two.cs");
    }

    [Fact]
    public void NoMatches_SaysSo()
    {
        _tool.Execute("**/*.py").Should().Be("No files found.");
    }
}
