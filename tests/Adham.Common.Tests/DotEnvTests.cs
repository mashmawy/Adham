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
}
