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

    // As Unsloth Studio reports it (trimmed): the context length it loaded the model with.
    private const string Studio = """
        {"object":"list","data":[
          {"id":"unsloth/Qwen3.8-27B-GGUF","object":"model","loaded":false},
          {"id":"unsloth/Qwen3.6-35B-A3B-MTP-GGUF","object":"model","context_length":32768,"max_context_length":8192,"native_context_length":262144,"loaded":true}
        ]}
        """;

    [Fact]
    public void UnslothStudio_ReportsTheLoadedContextLength()
    {
        ChatClientFactory.PickModel(Studio, modelId: null)
            .Should().Be(new ServerModel("unsloth/Qwen3.6-35B-A3B-MTP-GGUF", 32768));
    }

    [Fact]
    public void ModelAskedForByName_IsPickedEvenWhenNotLoaded()
    {
        ChatClientFactory.PickModel(Studio, "unsloth/Qwen3.8-27B-GGUF")
            .Should().Be(new ServerModel("unsloth/Qwen3.8-27B-GGUF", ContextLength: null));
    }

    [Fact]
    public void ModelAskedForByName_IsUsedEvenWhenNotListed()
    {
        ChatClientFactory.PickModel(Studio, "my-model").Should().Be(new ServerModel("my-model", ContextLength: null));
    }

    [Fact]
    public void ServerWithoutContextLength_ReportsNone()
    {
        const string json = """{"object":"list","data":[{"id":"qwen-local","object":"model"}]}""";

        ChatClientFactory.PickModel(json, modelId: null).Should().Be(new ServerModel("qwen-local", ContextLength: null));
    }
}
