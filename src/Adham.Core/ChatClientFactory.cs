using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Adham.Core;

// Any OpenAI-compatible server works: Unsloth Studio, llama.cpp, LM Studio, vLLM, ...
public static class ChatClientFactory
{
    public static IChatClient Build(Uri baseUrl, string apiKey, string modelId)
    {
        var openAi = CreateClient(baseUrl, apiKey);
        return new ChatClientBuilder(openAi.GetChatClient(modelId).AsIChatClient()).Build();
    }

    // The model currently loaded on the server: first entry of GET /v1/models.
    public static async Task<string?> GetLoadedModelAsync(Uri baseUrl, string apiKey, CancellationToken ct)
    {
        var models = await CreateClient(baseUrl, apiKey).GetOpenAIModelClient().GetModelsAsync(ct).ConfigureAwait(false);
        return models.Value.FirstOrDefault()?.Id;
    }

    private static OpenAIClient CreateClient(Uri baseUrl, string apiKey) =>
        new(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = baseUrl });
}
