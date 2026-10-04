using Adham.Core;
using FluentAssertions;
using Xunit;

namespace Adham.Core.Tests;

public class PickLoadedModelTests
{
    [Fact]
    public void UnslothStudio_PicksTheModelMarkedLoaded_NotTheFirstListed()
    {
        const string json = """
            {"object":"list","data":[
              {"id":"unsloth/Qwen3.6-35B-A3B-MTP-GGUF","object":"model","loaded":false},
              {"id":"unsloth/Qwen3.8-27B-GGUF","object":"model","loaded":true}
            ]}
            """;

        ChatClientFactory.PickLoadedModel(json).Should().Be("unsloth/Qwen3.8-27B-GGUF");
    }

    [Fact]
    public void UnslothStudio_WithNothingLoaded_ReturnsNull()
    {
        const string json = """
            {"object":"list","data":[
              {"id":"unsloth/Qwen3.6-35B-A3B-MTP-GGUF","object":"model","loaded":false},
              {"id":"unsloth/Qwen3.8-27B-GGUF","object":"model","loaded":false}
            ]}
            """;

        ChatClientFactory.PickLoadedModel(json).Should().BeNull();
    }

    [Fact]
    public void ServerWithoutLoadedFlag_PicksTheFirstModel()
    {
        const string json = """{"object":"list","data":[{"id":"qwen-local","object":"model"},{"id":"other","object":"model"}]}""";

        ChatClientFactory.PickLoadedModel(json).Should().Be("qwen-local");
    }

    [Fact]
    public void EmptyList_ReturnsNull()
    {
        ChatClientFactory.PickLoadedModel("""{"object":"list","data":[]}""").Should().BeNull();
    }
}
