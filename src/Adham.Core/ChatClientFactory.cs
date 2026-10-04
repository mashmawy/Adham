using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
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

    // The model the server can answer with right now, from GET /v1/models.
    public static async Task<string?> GetLoadedModelAsync(Uri baseUrl, string apiKey, CancellationToken ct)
    {
        // Raw JSON on purpose: the SDK's model type drops Unsloth Studio's "loaded" flag.
        var result = await CreateClient(baseUrl, apiKey).GetOpenAIModelClient()
            .GetModelsAsync(new RequestOptions { CancellationToken = ct }).ConfigureAwait(false);
        return PickLoadedModel(result.GetRawResponse().Content.ToString());
    }

    // Unsloth Studio lists every downloaded model and marks the one in memory with "loaded": true.
    // Servers without that flag list only what they serve, so their first entry is the answer.
    internal static string? PickLoadedModel(string modelsJson)
    {
        using var doc = JsonDocument.Parse(modelsJson);
        var models = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

        var hasLoadedFlag = models.Exists(m => m.TryGetProperty("loaded", out _));
        var pick = hasLoadedFlag
            ? models.Find(m => m.TryGetProperty("loaded", out var loaded) && loaded.ValueKind == JsonValueKind.True)
            : models.FirstOrDefault();

        return pick.ValueKind == JsonValueKind.Object ? pick.GetProperty("id").GetString() : null;
    }

    private static OpenAIClient CreateClient(Uri baseUrl, string apiKey) =>
        new(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = baseUrl });
}
