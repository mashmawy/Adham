using Adham.Common.Env;
using FluentAssertions;
using Xunit;

namespace Adham.Common.Tests;

public class AdhamEnvironmentTests
{
    [Fact]
    public void BaseUrl_DefaultsToLocalUnslothStudio()
    {
        var env = new AdhamEnvironment(_ => null);
        env.BaseUrl.Should().Be(new Uri("http://localhost:8888/v1"));
    }

    [Fact]
    public void BaseUrl_HonorsAdhamBaseUrl()
    {
        var env = new AdhamEnvironment(name => name == "ADHAM_BASE_URL" ? "http://gpu:8000/v1" : null);
        env.BaseUrl.Should().Be(new Uri("http://gpu:8000/v1"));
    }

    [Fact]
    public void ApiKeyAndModel_ComeFromEnvironment()
    {
        var env = new AdhamEnvironment(name => name switch
        {
            "ADHAM_API_KEY" => "sk-unsloth-test",
            "ADHAM_MODEL" => "qwen",
            _ => null,
        });

        env.ApiKey.Should().Be("sk-unsloth-test");
        env.Model.Should().Be("qwen");
    }
}
