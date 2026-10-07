using Adham.Cli;
using Adham.Common.Env;
using Adham.Core;
using Adham.Tools.Abstractions;
using Adham.Tools.Files;
using Adham.Tools.Search;
using Adham.Tools.Shell;
using Microsoft.Extensions.AI;

// Windows consoles often start in a legacy code page, which turns "⚙", "→" (and non-Latin text) into "?".
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

var env = AdhamEnvironment.FromProcessAndDotEnv(Directory.GetCurrentDirectory());
if (string.IsNullOrWhiteSpace(env.ApiKey))
{
    Console.Error.WriteLine($"Set ADHAM_API_KEY to your Unsloth Studio key (Settings → API, starts with sk-unsloth-): in {Adham.Common.Paths.AdhamPaths.UserDotEnv()} for every folder, in a project .env, or as an environment variable.");
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
var reads = new FileReadTracker();
var approver = ConsoleApprover.ForConsole();
var shell = new ShellTool(Shell.ForThisMachine(), cwd, approver);
var tools = new ToolRegistry([
    new ReadTool(cwd, reads),
    new GlobTool(cwd),
    new EditTool(cwd, reads, approver),
    new WriteTool(cwd, reads, approver),
    shell,
]);

using var client = ChatClientFactory.Build(env.BaseUrl, env.ApiKey, model);
var session = new AgentSession(
    client,
    new ChatOptions { ModelId = model, Tools = tools.AsAITools() },
    systemPrompt: $"""
        You are Adham, a concise coding assistant running on the user's machine.
        Working directory: {cwd.Path}
        Use the Glob and Read tools to look at the code before answering questions about it. Don't guess file contents.
        To change a file, Read it first, then prefer Edit for targeted changes and Write for new files.
        Run builds, tests and git with the {shell.Name} tool; after changing code, run the tests to check your fix.
        The user approves every change and every command that isn't read-only; if they decline, ask what they want instead.
        """);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"adham · {model} @ {env.BaseUrl}");
WriteDim($"working in {cwd.Path} · tools: {string.Join(", ", tools.All.Select(t => t.Name))} · /clear to start over · /exit to quit\n");
while (!cts.IsCancellationRequested)
{
    Console.Write("\n> ");
    var input = Console.ReadLine();
    if (input is null || input.Trim() == "/exit") break;
    if (string.IsNullOrWhiteSpace(input)) continue;
    if (input.Trim() == "/clear")
    {
        session.Clear();
        reads.Clear(); // a fresh conversation has read nothing, so edits need fresh reads
        WriteDim("(conversation cleared)\n");
        continue;
    }

    try
    {
        await foreach (var update in session.SendAsync(input, cts.Token))
        {
            foreach (var call in update.Contents.OfType<FunctionCallContent>())
                WriteDim($"\n  ⚙ {ToolCallText.Format(call.Name, call.Arguments, cwd)}\n");
            Console.Write(update.Text);
        }
        Console.WriteLine();
        if (session.LastTurn?.Note is { } note)
            WriteDim($"  ({note})\n");
    }
    catch (Exception ex) when (ServerErrors.IsServerError(ex))
    {
        Console.Error.WriteLine($"Model server error at {env.BaseUrl}: {ServerErrors.Describe(ex)}");
        if (ServerErrors.IsContextOverflow(ex))
            Console.Error.WriteLine("The conversation no longer fits the model's context window. Type /clear to start over, or raise Context Length in Unsloth Studio.");
    }
    catch (OperationCanceledException)
    {
        break;
    }
}
return 0;

static void WriteDim(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write(text);
    Console.ResetColor();
}
