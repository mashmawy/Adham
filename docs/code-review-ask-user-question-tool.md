# Code review: `ask-user-question-tool` (1 commit, c760491)

Reviewed 2026-10-09.

## Summary

This adds an `AskUser` tool so the model can ask the user a freeform question and get the answer back as text. It adds a new `IQuestionApprover` interface, implements it in `ConsoleApprover`, and registers the tool in `Program.cs`. The change is small and clean, and it builds with 0 warnings. All 7 test projects pass (13 tests in `Adham.Tools.Abstractions.Tests`, including the 7 new ones). There are a few problems with behavior and with the project's rules, so they should be fixed before merging.

## Issues

| # | File | Line | Issue | Severity |
|---|------|------|-------|----------|
| 1 | `src/Adham.Cli/ConsoleApprover.cs` | 29–37 | **The model can't tell "no answer" from an empty answer.** Cancellation, Ctrl+C during `ReadLine` (returns null) and EOF on redirected stdin all become `""`. The model then gets `User response: ` and will likely just guess. Return distinct text instead, e.g. `"The user gave no answer."`, and probably `Trim()` the answer. | 🟠 Medium |
| 2 | `tests/Adham.Cli.Tests/ConsoleApproverTests.cs` | – | **No tests for `ConsoleApprover.AskUserAsync`.** CLAUDE.md says every behavior has a test. Nothing covers the prompt format, the empty/EOF case or the cancelled-token case. The existing `StringReader`/`StringWriter` pattern there makes these easy to add. | 🟠 Medium |
| 3 | `src/Adham.Tools.Abstractions/AskUserTool.cs` | – | **A concrete tool sits in the abstractions project.** Every other tool has its own project (`Tools.Files`, `Tools.Search`, `Tools.Shell`). Putting `AskUserTool` in `Abstractions` breaks the "one project per responsibility" layout. The design doc even notes it could go in its own project. Move it to something like `Adham.Tools.Interaction`. | 🟡 Low–Med |
| 4 | `src/Adham.Tools.Abstractions/IQuestionApprover.cs` | 3–6 | **Misleading name.** Nothing gets approved here; it asks a question and returns an answer. Its comment ("Asked before returning a freeform user answer to the model") also doesn't describe what it does. Rename it to something like `IUserQuestioner` or `IQuestionAsker`. | 🟡 Low |
| 5 | `docs/decisions.md` | – | **No decision entry.** Several choices are non-obvious: `IsReadOnly = true`, a separate interface instead of extending the approvers, and a single-line answer. CLAUDE.md requires an entry for each. Most of this is in `docs/ask-user-question-tool.md`, but that file is a plan that mostly repeats the code and will drift from it. Put the reasoning in `decisions.md` and consider dropping the plan doc. | 🟡 Low |
| 6 | `CLAUDE.md` | Layout | The project list doesn't mention `AskUserTool` or `IQuestionApprover`. | 🟢 Nit |
| 7 | commit c760491 | – | The message `Add AskUser tool…` isn't a conventional commit. It should be something like `feat(tools): add AskUser tool…`. | 🟢 Nit |

## Suggestions

| # | File | Line | Suggestion | Category |
|---|------|------|------------|----------|
| 1 | `AskUserTool.cs` | 2 | Remove `using Adham.Tools.Abstractions;`. The file is already in that namespace. | Style |
| 2 | `Program.cs` / `ConsoleApprover.cs` | – | The question appears twice on screen: once in the grey `⚙ AskUser(question: …)` line, then again as `? …`. Either have `ToolCallText` skip arguments for `AskUser`, or drop the question from the prompt. | UX |
| 3 | `AskUserToolTests.cs` | – | The tests call `AskAsync` directly. Add one test through `AsAIFunction().InvokeAsync(...)` to cover the `ToolFunction` wiring (plain-text result, snake_case `question` parameter). | Tests |
| 4 | `SystemPrompt.cs` | 11, 21 | The prompt already tells the model to "ask for clarification" and "ask what they want instead". Small local models may now call `AskUser` instead of searching first. Before, these asks ended the turn with a normal reply; now they happen inside the tool loop. Earlier decisions in `decisions.md` were based on live runs, so measure this the same way. | Behavior |
| 5 | `ConsoleApprover.cs` | 34 | Multi-line answers aren't possible. Fine for now since it's out of scope, but say so in the tool description so the model asks one thing at a time. | Docs |

## What looks good

- Keeping questions out of the yes/no approval interfaces is the right split.
- The tool follows the existing conventions: `ToolFunction.Create`, an `Error:`-prefixed message for empty input, and `ConfigureAwait(false)`.
- The tests cover null, empty and whitespace questions, and check the schema.
- `ConsoleApprover` checks the cancellation token before blocking on `ReadLine`.

## Verdict

**Request changes**: fix #1 and #2 (both are quick), and decide on #3 before more code depends on where the tool lives. The rest can be done in the same pass.
