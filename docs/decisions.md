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

- **Decision**: When `ADHAM_MODEL` is unset, the CLI asks the server (`GET /v1/models`) and uses the model it can answer with right now: the one Unsloth Studio marks `"loaded": true`, or the first entry on servers that don't send that flag. (Corrected 2026-10-04: the first version took the first entry, but Studio lists every *downloaded* model, so it picked unloaded ones.)
- **Why**: In Unsloth Studio you pick the model in the UI; making the user copy its id into an environment variable as well is friction with no benefit.
- **Alternatives considered**: A hard-coded default model id, which breaks as soon as a different model is loaded.

## 2026-10-04: Treat the OpenAI SDK's retry failure as a server error

- **Decision**: `ServerErrors` recognises `HttpRequestException`, `ClientResultException`, and an `AggregateException` made only of those, and reports the first inner message on one line.
- **Why**: With the server down, the SDK retries 4 times and then throws `AggregateException`, not `HttpRequestException`. Catching only `HttpRequestException` crashed the CLI with a stack trace. An `AggregateException` holding anything else is still allowed to surface, so real bugs aren't hidden.
- **Alternatives considered**: Catching all exceptions in the chat loop, which would also hide programming errors.

## 2026-10-04: Tools return plain text to the model

- **Decision**: Tools build their `AIFunction` with `ToolFunction.Create`, whose `MarshalResult` passes the returned string through unchanged.
- **Why**: By default MEAI JSON-serializes return values, so a file read reached the model as one escaped string (`"     1→using System;
     2→..."`). Seen on the wire with a fake server. Large hosted models cope; for a local model it's needless noise in every tool result.
- **Alternatives considered**: Returning structured JSON from tools, which costs more tokens and reads worse for small models.

## 2026-10-04: Tools take paths relative to the working directory

- **Decision**: `Read` and `Glob` accept relative paths (resolved against the folder `adham` runs in), and `Glob` returns relative paths.
- **Why**: Short relative paths cost fewer tokens and are easier for small models to copy correctly into the next call than long absolute ones.
- **Alternatives considered**: Requiring absolute paths, which models often get wrong.

## 2026-10-04: Keep tool calls and results in the conversation history

- **Decision**: `AgentSession` appends every message of a turn, including the assistant's tool calls and the tool results, not just the final text.
- **Why**: Otherwise the next turn has no record of which files the model already read and what was in them, so it reads them again or guesses.
- **Alternatives considered**: Keeping only the final answer (smaller context, but forgetful), or a sliding window over old tool results. That window is the likely answer once long sessions hit the context limit, and is left for later.

## 2026-10-04: Local settings live in a git-ignored .env file

- **Decision**: The CLI reads `ADHAM_*` settings from the nearest `.env` file (the working directory or any parent), with real environment variables taking precedence. `.env` is git-ignored; `.env.example` is committed.
- **Why**: Typing the API key into every new terminal is friction, and putting it in a tracked file is a leak waiting to happen. Walking up the parents means `adham` finds the file from any subfolder of the repo.
- **Alternatives considered**: `dotnet user-secrets` (tied to one project and needs the SDK at run time), or a user-wide `~/.adham` config (the right home once there are more settings than a key).
