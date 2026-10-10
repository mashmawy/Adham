# Ask-User-Question Tool

## Goal

Let the agent ask the user freeform questions during a conversation and get back their response as text. Useful when the model needs information only the user can provide (preferences, clarification, project details).

## Design Decisions

### 1. New interface: `IQuestionApprover`

Don't pollute existing approver interfaces (`IChangeApprover`, `ICommandApprover`). The question flow is fundamentally different from an approval gate — it's a request for information, not a yes/no on a change or command.

```csharp
public interface IQuestionApprover
{
    ValueTask<string> AskUserAsync(string question, CancellationToken cancellationToken);
}
```

### 2. `ConsoleApprover` implements it

`ConsoleApprover` already reads from `Console.In` and writes to `Console.Out`. It just needs one additional method:

```csharp
public ValueTask<string> AskUserAsync(string question, CancellationToken cancellationToken)
{
    if (cancellationToken.IsCancellationRequested)
        return ValueTask.FromResult("");  // don't block on ReadLine() if cancelled

    output.Write($"\n  ? {question}\n  > ");
    var answer = input.ReadLine() ?? "";
    return ValueTask.FromResult(answer);
}
```

The `cancellationToken` can't cancel a blocking `ReadLine()` call, but we check it upfront to avoid stalling the conversation if the user pressed Ctrl+C.

### 3. `AskUserTool` — a new tool

Location: `src/Adham.Tools.Abstractions/AskUserTool.cs` (co-located with other simple tools, or in its own project if the team prefers).

Properties:
- **Name:** `"AskUser"`
- **IsReadOnly:** `true` — asks a question but doesn't modify files, run commands, or change state. The model should be allowed to call it without approval.
- **Description:** Instructs the model to keep questions specific and concise.
- **Parameters:** A single required `string question`.

Implementation:

```csharp
public sealed class AskUserTool(IQuestionApprover approver) : ITool
{
    public string Name => "AskUser";
    public bool IsReadOnly => true;
    public string Description =>
        "Ask the user a question and wait for their response. Use it when you need information " +
        "only the user can provide (e.g., project setup details, preferences, clarification). " +
        "Keep questions specific and concise. The user's answer is returned as a string.";

    public AIFunction AsAIFunction() =>
        ToolFunction.Create(AskAsync, Name, Description);

    internal async Task<string> AskAsync(
        [Description("The question to ask the user. Be specific and concise.")] string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return "Error: question is required.";

        var answer = await approver.AskUserAsync(question, cancellationToken).ConfigureAwait(false);
        return $"User response: {answer}";
    }
}
```

### 4. Wiring in `Program.cs`

Add it to the tool registry alongside the existing tools:

```csharp
var tools = new ToolRegistry([
    new ReadTool(cwd, reads),
    new GlobTool(cwd),
    new GrepTool(cwd),
    new EditTool(cwd, reads, approver),
    new WriteTool(cwd, reads, approver),
    shell,
    new AskUserTool(approver),   // ← ConsoleApprover now also implements IQuestionApprover
]);
```

### 5. Tests

Create `tests/Adham.Tools.Abstractions.Tests/AskUserToolTests.cs` with a simple mock:

```csharp
private sealed class FakeQuestionApprover : IQuestionApprover
{
    public string? LastQuestion;
    public string Answer = "test answer";

    public ValueTask<string> AskUserAsync(string question, CancellationToken cancellationToken)
    {
        LastQuestion = question;
        return ValueTask.FromResult(Answer);
    }
}
```

Test cases:
- Returns the user's answer prefixed with `"User response: "`
- Returns `"Error: question is required."` for empty/null input
- `Name == "AskUser"`, `IsReadOnly == true`
- Schema contains `"question"` as a required parameter

## Files Changed

| File | Action |
|------|--------|
| `src/Adham.Tools.Abstractions/IQuestionApprover.cs` | **New** |
| `src/Adham.Cli/ConsoleApprover.cs` | Modified — add `IQuestionApprover` impl |
| `src/Adham.Tools.Abstractions/AskUserTool.cs` | **New** |
| `src/Adham.Cli/Program.cs` | Modified — register the tool |
| `tests/Adham.Tools.Abstractions.Tests/AskUserToolTests.cs` | **New** |

## What's out of scope

- System prompt update (the model already knows how to ask users; the tool description handles it)
- `ToolResultText.Summarize` — the result is short and visible to the model in context; no screen summary needed
- Timeout or retry logic for unanswered questions
- Async streaming (user types a single line, gets returned as-is)
