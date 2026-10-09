# Ask-User-Question Tool — Implementation Summary

## What was built

A new tool (`AskUser`) that lets the agent ask the user freeform questions during a conversation and get back their response as text. Useful when the model needs information only the user can provide (preferences, clarification, project details).

## Design decisions

| Decision | Rationale |
|----------|-----------|
| New `IQuestionApprover` interface | Keeps question flow separate from approval gates (`IChangeApprover`, `ICommandApprover`). Asking for information is fundamentally different from saying yes/no to a change or command. |
| `ConsoleApprover` implements it | Reuses the existing `TextReader`/`TextWriter` pattern. Checks `cancellationToken.IsCancellationRequested` before blocking on `ReadLine()` to avoid stalling if the user pressed Ctrl+C. |
| `AskUserTool.IsReadOnly = true` | Asking a question doesn't modify files, run commands, or change state. The model can call it without approval. |
| Return format `"User response: {answer}"` | Lets the model distinguish between empty input and an error. Also keeps the prefix consistent for parsing. |

## Files changed (7 files)

| File | Action |
|------|--------|
| `src/Adham.Tools.Abstractions/IQuestionApprover.cs` | **New** — interface with `AskUserAsync(string, CancellationToken)` |
| `src/Adham.Tools.Abstractions/AskUserTool.cs` | **New** — tool implementing `ITool` (`Name="AskUser"`, `IsReadOnly=true`) |
| `src/Adham.Cli/ConsoleApprover.cs` | Modified — added `IQuestionApprover` implementation |
| `src/Adham.Cli/Program.cs` | Modified — registered `new AskUserTool(approver)` in the tool registry |
| `tests/Adham.Tools.Abstractions.Tests/AskUserToolTests.cs` | **New** — 7 test cases (name, read-only flag, answer prefixing, error cases, schema validation) |
| `src/Adham.Tools.Abstractions/Adham.Tools.Abstractions.csproj` | Modified — added `InternalsVisibleTo` for tests |
| `docs/ask-user-question-tool.md` | **New** — design doc with rationale and implementation plan |

## Test results

All 277 tests pass across every project (0 failures, 0 skipped).

## Git commit

```
c760491 Add AskUser tool for freeform user questions
 Author: Adham <adham@agent>
 7 files changed, 270 insertions(+), 1 deletion(-)
 Branch: ask-user-question-tool
 Pushed to: origin/ask-user-question-tool
```

## Out of scope (not implemented)

- System prompt update — the tool description already instructs the model
- `ToolResultText.Summarize` — the result is short and visible in context
- Timeout or retry logic for unanswered questions
- Async streaming — user types a single line, returned as-is
