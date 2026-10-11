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

ServerModel served;
try
{
    served = await ChatClientFactory.GetModelAsync(env.BaseUrl, env.ApiKey, env.Model, CancellationToken.None)
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
    new GrepTool(cwd),
    new EditTool(cwd, reads, approver),
    new WriteTool(cwd, reads, approver),
    shell,
    new AskUserTool(approver),
]);

var model = served.Id;
var atLineStart = true; // so the timing line after a reply starts on a line of its own
var modelCalls = 0;
using var client = ChatClientFactory.Build(env.BaseUrl, env.ApiKey, model, served.ContextLength, onModelCall: timing =>
{
    modelCalls++;
    WriteDim($"{(atLineStart ? "" : "\n")}  {TimingText.Format(timing)}\n");
});
var session = new AgentSession(
    client,
    new ChatOptions { ModelId = model, Tools = tools.AsAITools() },
    systemPrompt: SystemPrompt.Build(cwd.Path, shell.Name),
    contextWindow: served.ContextLength);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"adham · {model}{(served.ContextLength is { } window ? $" ({window:N0}-token context)" : "")} @ {env.BaseUrl}");
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
        var callNames = new Dictionary<string, string>();
        var contextShown = session.ContextTokens;
        var turnClock = System.Diagnostics.Stopwatch.StartNew();
        modelCalls = 0;
        await foreach (var update in session.SendAsync(input, cts.Token))
        {
            foreach (var content in update.Contents)
            {
                if (content is FunctionCallContent call)
                {
                    callNames[call.CallId] = call.Name;
                    WriteDim($"\n  ⚙ {ToolCallText.Format(call.Name, call.Arguments, cwd)}\n");
                }
                else if (content is FunctionResultContent result && callNames.TryGetValue(result.CallId, out var tool))
                {
                    foreach (var line in ToolResultText.Summarize(tool, result.Result))
                        WriteDim($"    │ {line}\n");

                    // Seen live: one long turn went from 22% to full with nothing on screen in between.
                    if (session.ContextTokens != contextShown && ContextText.Format(session.ContextTokens, served.ContextLength) is { } sofar)
                    {
                        WriteDim($"    │ {sofar}\n");
                        contextShown = session.ContextTokens;
                    }
                }
            }
            Write(update.Text);
        }
        if (!atLineStart)
            Write("\n");
        if (session.LastTurn?.Note is { } note)
            WriteDim($"  ({note})\n");
        var turn = TimingText.Turn(turnClock.Elapsed, modelCalls);
        WriteDim(ContextText.Format(session.ContextTokens, served.ContextLength) is { } context ? $"  {turn} · {context}\n" : $"  {turn}\n");
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

void Write(string text)
{
    if (text.Length == 0)
        return;
    Console.Write(text);
    atLineStart = text[^1] == '\n';
}

void WriteDim(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Write(text);
    Console.ResetColor();
}
