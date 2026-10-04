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

- `Adham.Cli`: console chat loop (`adham` executable), `ServerErrors`.
- `Adham.Core`: `ChatClientFactory` (OpenAI-compatible client, loaded-model lookup), `AgentSession` (streaming, multi-turn history).
- `Adham.Common`: `AdhamEnvironment` (`ADHAM_API_KEY`, `ADHAM_BASE_URL`, `ADHAM_MODEL`).

Tests use `FakeChatClient` (in `Adham.Core.Tests`) instead of a real model.

## Rules

- Warnings are errors and analyzers are on (`Directory.Build.props`). Fix the code; don't suppress or relax rules.
- All package versions live in `Directory.Packages.props`. No `Version` attributes in project files.
- Every behavior has a test.
- Add a `docs/decisions.md` entry (date, decision, why, alternatives) for each non-obvious choice.
- Conventional commits (`feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`).
- Never commit secrets: API keys go in environment variables or user secrets, never in tracked files.

## Reference material

Atlas, the earlier prototype, is at `C:\Users\speed\source\repos\Atlas`. It is read-only reference: read it and use `git -C <atlas> show <sha>:<path>`, but never modify it.
