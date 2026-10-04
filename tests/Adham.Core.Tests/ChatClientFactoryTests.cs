using Adham.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Core.Tests;

public class ChatClientFactoryTests
{
    [Fact]
    public void Build_TargetsTheConfiguredServerAndModel()
    {
        using var client = ChatClientFactory.Build(new Uri("http://localhost:8888/v1"), "sk-test", "qwen");

        var metadata = client.GetService<ChatClientMetadata>();

        metadata.Should().NotBeNull();
        metadata!.ProviderUri.Should().Be(new Uri("http://localhost:8888/v1"));
        metadata.DefaultModelId.Should().Be("qwen");
    }
}
