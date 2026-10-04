using Adham.Common.Env;
using Adham.Core;
using Microsoft.Extensions.AI;

var env = new AdhamEnvironment();
if (string.IsNullOrWhiteSpace(env.ApiKey))
{
    Console.Error.WriteLine("Set ADHAM_API_KEY to your Unsloth Studio key (Settings → API, starts with sk-unsloth-).");
    return 1;
}

string model;
try
{
    model = env.Model
        ?? await ChatClientFactory.GetLoadedModelAsync(env.BaseUrl, env.ApiKey, CancellationToken.None)
        ?? throw new InvalidOperationException("no model is loaded; load one in Unsloth Studio first");
}
catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
{
    Console.Error.WriteLine($"Cannot get a model from {env.BaseUrl}: {ex.Message}");
    return 1;
}

using var client = ChatClientFactory.Build(env.BaseUrl, env.ApiKey, model);
var session = new AgentSession(
    client,
    new ChatOptions { ModelId = model },
    systemPrompt: "You are Adham, a concise coding assistant running on the user's machine.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"adham · {model} @ {env.BaseUrl} · /exit to quit");
while (!cts.IsCancellationRequested)
{
    Console.Write("\n> ");
    var input = Console.ReadLine();
    if (input is null || input.Trim() == "/exit") break;
    if (string.IsNullOrWhiteSpace(input)) continue;

    try
    {
        await foreach (var update in session.SendAsync(input, cts.Token))
            Console.Write(update.Text);
        Console.WriteLine();
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine($"Model server error at {env.BaseUrl}: {ex.Message}");
    }
    catch (OperationCanceledException)
    {
        break;
    }
}
return 0;
