using Adham.Common.Env;
using FluentAssertions;
using Xunit;

namespace Adham.Common.Tests;

public sealed class DotEnvTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("adham-dotenv-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Parse_ReadsKeyValues_SkippingCommentsAndBlanks_AndStrippingQuotes()
    {
        var values = DotEnv.Parse([
            "# local dev settings",
            "",
            "ADHAM_API_KEY=sk-unsloth-abc",
            "ADHAM_MODEL = \"unsloth/Qwen\"",
            "ADHAM_BASE_URL='http://gpu:8000/v1'",
            "not a setting",
        ]);

        values.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["ADHAM_API_KEY"] = "sk-unsloth-abc",
            ["ADHAM_MODEL"] = "unsloth/Qwen",
            ["ADHAM_BASE_URL"] = "http://gpu:8000/v1",
        });
    }

    [Fact]
    public void Find_WalksUpFromASubfolder()
    {
        var envFile = Path.Combine(_root, DotEnv.FileName);
        File.WriteAllText(envFile, "ADHAM_API_KEY=x");
        var deep = Directory.CreateDirectory(Path.Combine(_root, "src", "App")).FullName;

        DotEnv.Find(deep).Should().Be(envFile);
    }

    [Fact]
    public void Load_WithNoFile_IsEmpty()
    {
        DotEnv.Load(null).Should().BeEmpty();
    }

    [Fact]
    public void Layered_FileFillsIn_WhatTheProcessDoesNotSet()
    {
        var file = DotEnv.Parse(["ADHAM_API_KEY=from-file", "ADHAM_MODEL=file-model"]);

        var env = AdhamEnvironment.Layered(name => name == "ADHAM_MODEL" ? "process-model" : null, file);

        env.ApiKey.Should().Be("from-file");
        env.Model.Should().Be("process-model");
    }

    [Fact]
    public void Layered_EmptyProcessVariable_FallsBackToTheFile()
    {
        var file = DotEnv.Parse(["ADHAM_API_KEY=from-file"]);

        var env = AdhamEnvironment.Layered(_ => "", file);

        env.ApiKey.Should().Be("from-file");
    }

    [Fact]
    public void Layered_ProjectFileWins_OverTheUserFile()
    {
        var project = DotEnv.Parse(["ADHAM_MODEL=project-model"]);
        var user = DotEnv.Parse(["ADHAM_MODEL=user-model", "ADHAM_API_KEY=user-key"]);

        var env = AdhamEnvironment.Layered(_ => null, project, user);

        env.Model.Should().Be("project-model");
        env.ApiKey.Should().Be("user-key"); // the user file still fills in what the project doesn't set
    }
}

public class AdhamPathsTests
{
    [Fact]
    public void UserConfigDir_DefaultsToDotAdhamInTheHomeFolder()
    {
        var dir = Adham.Common.Paths.AdhamPaths.UserConfigDir(_ => null, Path.Combine("home", "u"));

        dir.Should().Be(Path.Combine("home", "u", ".adham"));
    }

    [Fact]
    public void UserConfigDir_HonorsAdhamHome()
    {
        var dir = Adham.Common.Paths.AdhamPaths.UserConfigDir(n => n == "ADHAM_HOME" ? "/custom" : null, "home");

        dir.Should().Be("/custom");
    }
}
