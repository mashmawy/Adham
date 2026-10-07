# Adham

Adham is a local-first coding agent for local LLMs. It talks to a local model (Unsloth Studio by default) through the OpenAI-compatible API. The interface is a plain console CLI for now; the UI comes later, so the core must never depend on a UI.

## Stack

- .NET 10 / C#
- Microsoft.Extensions.AI (MEAI) with `Microsoft.Extensions.AI.OpenAI` against an OpenAI-compatible backend
- xUnit + FluentAssertions for tests

## Layout

- `src/Adham.<Area>`: production projects, one project per responsibility.
- `tests/Adham.<Area>.Tests`: test projects, mirroring `src/`.
- `Adham.Common` is the leaf (depends on no other Adham project); `Adham.Cli` is the root (nothing depends on it).
- `docs/decisions.md`: the decision log.

Projects so far (dependencies point down):

- `Adham.Cli`: console chat loop (`adham` executable), registers the tools, prints tool calls; `ServerErrors`; `ConsoleApprover` (diff + y/N before file changes, command + y/N before commands), `DiffText`, `ToolCallText`.
- `Adham.Tools.Shell`: `ShellTool` (PowerShell on Windows, Bash elsewhere), `ShellRunner` (CliWrap), `CommandSplitter`, `DenyRules`, `ReadOnlyRules`. Every command: deny → read-only → ask via `ICommandApprover`.
- `Adham.Tools.Files`: `ReadTool`, `EditTool`, `WriteTool`, `FileReadTracker` (read-before-write rules). `Adham.Tools.Search`: `GlobTool`.
- `Adham.Core`: `ChatClientFactory` (OpenAI-compatible client with `UseFunctionInvocation()`, loaded-model lookup), `AgentSession` (streaming, history including tool calls and results). Core does not reference the tools.
- `Adham.Tools.Abstractions`: `ITool`, `ToolRegistry`, `ToolFunction`, `WorkingDirectory`, `IChangeApprover`/`FileChange`, `ICommandApprover`/`CommandRequest`.
- `Adham.Common`: `AdhamEnvironment` (`ADHAM_API_KEY`, `ADHAM_BASE_URL`, `ADHAM_MODEL`).

Tests use `FakeChatClient` (in `Adham.Core.Tests`) instead of a real model.

## Rules

- Warnings are errors and analyzers are on (`Directory.Build.props`). Fix the code; don't suppress or relax rules.
- All package versions live in `Directory.Packages.props`. No `Version` attributes in project files.
- Every behavior has a test.
- Tools build their `AIFunction` with `ToolFunction.Create` so results reach the model as plain text. Tool parameter names are snake_case (they are the schema the model sees). Tools accept paths relative to the working directory.
- Add a `docs/decisions.md` entry (date, decision, why, alternatives) for each non-obvious choice.
- Conventional commits (`feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`).
- Never commit secrets: API keys go in environment variables or user secrets, never in tracked files.
