# Decision log

Non-obvious choices made while building Adham, newest last. Each entry is dated and records:

- **Decision**: what was chosen.
- **Why**: the reason it was chosen.
- **Alternatives considered**: what else was on the table and why it lost.

## 2026-10-04: Backend is Unsloth Studio over the OpenAI-compatible API

- **Decision**: Talk to the model through the OpenAI-compatible API using `Microsoft.Extensions.AI.OpenAI`, with Unsloth Studio as the default local server.
- **Why**: One standard protocol works with any local server (Unsloth Studio, llama.cpp, LM Studio, vLLM), so the backend is a configuration choice, not a code change.
- **Alternatives considered**: Ollama's native API, which ties the agent to a single server.

## 2026-10-04: Console CLI first; the UI is deferred

- **Decision**: Ship a plain console CLI. Choose the real UI later.
- **Why**: The previous version's terminal UI was the biggest source of bugs, and the core must stay UI-agnostic so any UI can sit on top of it.
- **Alternatives considered**: Building a rich terminal UI from the start, as in the previous version.

## 2026-10-04: Strict build from commit one

- **Decision**: Warnings are errors and the .NET analyzers run at `latest-recommended`, with code style enforced in the build (`Directory.Build.props`).
- **Why**: Turning strictness on later means fixing a backlog of warnings; turning it on first keeps every commit clean.
- **Alternatives considered**: Default warning levels, tightening rules later.

## 2026-10-04: Central package management

- **Decision**: Every package version is pinned in `Directory.Packages.props`, with transitive pinning on. Project files reference packages without versions.
- **Why**: One place to see and update versions, and no drift between projects.
- **Alternatives considered**: Per-project `Version` attributes.

## 2026-10-04: Use the model the server has loaded unless one is named

- **Decision**: When `ADHAM_MODEL` is unset, the CLI asks the server (`GET /v1/models`) and uses the first model it reports.
- **Why**: In Unsloth Studio you pick the model in the UI; making the user copy its id into an environment variable as well is friction with no benefit.
- **Alternatives considered**: A hard-coded default model id, which breaks as soon as a different model is loaded.

## 2026-10-04: Treat the OpenAI SDK's retry failure as a server error

- **Decision**: `ServerErrors` recognises `HttpRequestException`, `ClientResultException`, and an `AggregateException` made only of those, and reports the first inner message on one line.
- **Why**: With the server down, the SDK retries 4 times and then throws `AggregateException`, not `HttpRequestException`. Catching only `HttpRequestException` crashed the CLI with a stack trace. An `AggregateException` holding anything else is still allowed to surface, so real bugs aren't hidden.
- **Alternatives considered**: Catching all exceptions in the chat loop, which would also hide programming errors.
