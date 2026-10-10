using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Adham.Core;

// Any OpenAI-compatible server works: Unsloth Studio, llama.cpp, LM Studio, vLLM, ...
public static class ChatClientFactory
{
    public static IChatClient Build(Uri baseUrl, string apiKey, string modelId, int? contextWindow = null)
    {
        var openAi = CreateClient(baseUrl, apiKey);
        return new ChatClientBuilder(openAi.GetChatClient(modelId).AsIChatClient())
            // The tool loop: when the model asks for a tool, run it, append the result, ask again.
            .UseFunctionInvocation()
            // Inside the loop, so each call it makes (not only each turn) sends shortened old tool output.
            .Use(inner => new ShortenedContextClient(inner, ShortenedContextClient.KeepCharsFor(contextWindow)))
            .Build();
    }

    // The model to talk to (the one asked for, or the one the server can answer with right now)
    // and its context window, from GET /v1/models.
    public static async Task<ServerModel?> GetModelAsync(Uri baseUrl, string apiKey, string? modelId, CancellationToken ct)
    {
        // Raw JSON on purpose: the SDK's model type drops Unsloth Studio's "loaded" and "context_length".
        var result = await CreateClient(baseUrl, apiKey).GetOpenAIModelClient()
            .GetModelsAsync(new RequestOptions { CancellationToken = ct }).ConfigureAwait(false);
        return PickModel(result.GetRawResponse().Content.ToString(), modelId);
    }

    internal static string? PickLoadedModel(string modelsJson) => PickModel(modelsJson, modelId: null)?.Id;

    // Unsloth Studio lists every downloaded model and marks the one in memory with "loaded": true.
    // Servers without that flag list only what they serve, so their first entry is the answer.
    // A model asked for by name is used even when the server doesn't list it.
    internal static ServerModel? PickModel(string modelsJson, string? modelId)
    {
        using var doc = JsonDocument.Parse(modelsJson);
        var models = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

        var hasLoadedFlag = models.Exists(m => m.TryGetProperty("loaded", out _));
        var pick = modelId is not null
            ? models.Find(m => m.GetProperty("id").GetString() == modelId)
            : hasLoadedFlag
                ? models.Find(m => m.TryGetProperty("loaded", out var loaded) && loaded.ValueKind == JsonValueKind.True)
                : models.FirstOrDefault();

        if (pick.ValueKind != JsonValueKind.Object)
            return modelId is null ? null : new ServerModel(modelId, ContextLength: null);

        // Unsloth Studio reports the context length it loaded the model with; other servers may not.
        int? contextLength = pick.TryGetProperty("context_length", out var length) && length.TryGetInt32(out var tokens) && tokens > 0
            ? tokens
            : null;
        return new ServerModel(pick.GetProperty("id").GetString()!, contextLength);
    }

    private static OpenAIClient CreateClient(Uri baseUrl, string apiKey) =>
        new(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = baseUrl });
}
