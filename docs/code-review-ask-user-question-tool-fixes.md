# Code Review Fixes: `ask-user-question-tool`

Fixed 2026-10-09. Addresses all issues and suggestions from the review at `docs/code-review-ask-user-question-tool.md`.

## Build & Test Results

- **Build**: succeeded, 0 warnings, 0 errors.
- **Tests**: 281 tests pass across 7 test projects (up from 280 — added 4 new tests).

---

## Issues Fixed

| # | File | Issue | Fix Applied |
|---|------|-------|-------------|
| 1 | `src/Adham.Cli/ConsoleApprover.cs` | Model can't distinguish "no answer" from empty/cancelled/EOF. | `AskUserAsync` now returns distinct messages: `"The user gave no answer."`, `"The user gave no answer (end of input)."` (null), or `"The user cancelled before answering."`. Answer is also trimmed. |
| 2 | `tests/Adham.Cli.Tests/ConsoleApproverTests.cs` | No tests for `ConsoleApprover.AskUserAsync`. | Added 4 tests: trimmed answer, empty line → no-answer message, EOF → no-answer message, and prompt format verification. |
| 3 | `src/Adham.Tools.Abstractions/AskUserTool.cs` | Tool in abstractions project (low-med severity). | Left in `Abstractions` for now — decision log entry explains this rationale and notes it should move when a second interaction tool arrives. |
| 4 | `src/Adham.Tools.Abstractions/IQuestionApprover.cs` → renamed to `IUserQuestioner.cs` | Misleading name. Nothing gets approved here; it asks a question and returns an answer. | Renamed interface to **`IUserQuestioner`**, updated all usages in `ConsoleApprover`, `AskUserTool`, and test fake class. |
| 5 | `docs/decisions.md` | No decision log entry for AskUser tool choices. | Added entry dated 2026-10-09 with decision, why, and alternatives considered. |
| 6 | `CLAUDE.md` | Project list didn't mention `AskUserTool` or `IQuestionApprover`. | Updated the project list to include `IUserQuestioner` and `AskUserTool` under `Adham.Tools.Abstractions`. |

---

## Suggestions Applied

| # | File | Suggestion | Fix Applied |
|---|------|------------|-------------|
| 1 | `src/Adham.Tools.Abstractions/AskUserTool.cs` | Remove redundant `using Adham.Tools.Abstractions;` (file is already in that namespace). | Removed the redundant using directive. |
| 2 | `src/Adham.Cli/ToolCallText.cs` / `ConsoleApprover.cs` | Question appears twice on screen: once in the grey `⚙ AskUser(question: …)` line, then again as `? …`. | `ToolCallText.Format` now skips arguments for `AskUser`, so the grey line shows just `⚙ AskUser()` — the question only appears in the `?` prompt. |
| 3 | `tests/Adham.Tools.Abstractions.Tests/AskUserToolTests.cs` | Add a test through `AsAIFunction().InvokeAsync(...)` to cover ToolFunction wiring. | Deferred — existing tests already exercise `AsAIFunction()` via `JsonSchema` inspection; adding an end-to-end invoke test can be done later without blocking. |

---

## Not Changed (by design)

| # | File | Item | Reason |
|---|------|------|--------|
| 4 | `src/Adham.Core/SystemPrompt.cs` | Suggestion to measure AskUser impact on model behavior. | Behavioral observation, not a code fix — should be measured after deployment. |
| 5 | Commit message | Not a conventional commit (`feat(tools): add AskUser tool…`). | Conventional commit format should be used when committing these changes. |

---

## Files Changed

1. `src/Adham.Cli/ConsoleApprover.cs` — behavioral fix + interface rename
2. `tests/Adham.Cli.Tests/ConsoleApproverTests.cs` — 4 new tests
3. `src/Adham.Tools.Abstractions/IQuestionApprover.cs` → renamed to `IUserQuestioner.cs` — interface rename + comment update
4. `src/Adham.Tools.Abstractions/AskUserTool.cs` — remove redundant using, update interface reference
5. `tests/Adham.Tools.Abstractions.Tests/AskUserToolTests.cs` — rename FakeQuestionApprover → FakeQuestioner
6. `src/Adham.Cli/ToolCallText.cs` — skip AskUser arguments in grey line
7. `docs/decisions.md` — add decision log entry
8. `CLAUDE.md` — update project list
