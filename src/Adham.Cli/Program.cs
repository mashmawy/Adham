using Adham.Cli;
using Adham.Common.Env;
using Adham.Core;
using Adham.Tools.Abstractions;
using Adham.Tools.Files;
using Adham.Tools.Search;
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
catch (Exception ex) when (ServerErrors.IsServerError(ex) || ex is InvalidOperationException)
{
    Console.Error.WriteLine($"Cannot get a model from {env.BaseUrl}: {ServerErrors.Describe(ex)}");
    return 1;
}

var cwd = new WorkingDirectory(Directory.GetCurrentDirectory());
var tools = new ToolRegistry([new ReadTool(cwd), new GlobTool(cwd)]);

using var client = ChatClientFactory.Build(env.BaseUrl, env.ApiKey, model);
var session = new AgentSession(
    client,
    new ChatOptions { ModelId = model, Tools = tools.AsAITools() },
    systemPrompt: $"""
        You are Adham, a concise coding assistant running on the user's machine.
        Working directory: {cwd.Path}
        Use the Glob and Read tools to look at the code before answering questions about it. Don't guess file contents.
        """);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"adham · {model} @ {env.BaseUrl} · tools: {string.Join(", ", tools.All.Select(t => t.Name))} · /exit to quit");
while (!cts.IsCancellationRequested)
{
    Console.Write("\n> ");
    var input = Console.ReadLine();
    if (input is null || input.Trim() == "/exit") break;
    if (string.IsNullOrWhiteSpace(input)) continue;

    try
    {
        await foreach (var update in session.SendAsync(input, cts.Token))
        {
            foreach (var call in update.Contents.OfType<FunctionCallContent>())
                WriteDim($"\n  ⚙ {call.Name}({FormatArgs(call.Arguments)})\n");
            Console.Write(update.Text);
        }
        Console.WriteLine();
    }
    catch (Exception ex) when (ServerErrors.IsServerError(ex))
    {
        Console.Error.WriteLine($"Model server error at {env.BaseUrl}: {ServerErrors.Describe(ex)}");
    }
    catch (OperationCanceledException)
    {
        break;
    }
}
return 0;

static string FormatArgs(IDictionary<string, object?>? args) =>
    args is null ? "" : string.Join(", ", args.Select(a => $"{a.Key}: {a.Value}"));

static void WriteDim(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write(text);
    Console.ResetColor();
}
