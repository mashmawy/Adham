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
- **Why**: A rich terminal UI is a large, hard-to-test surface and a common source of bugs; building it first would slow everything else down. The core must stay UI-agnostic so any UI can sit on top of it.
- **Alternatives considered**: Building a rich terminal UI from the start.

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

## 2026-10-04: A failed turn is rolled back

- **Decision**: If a turn fails or is cancelled, `AgentSession` removes everything that turn added (the user message and any partial exchange).
- **Why**: Found live: Studio rejected a request with `context_length_exceeded` (9,262 tokens vs an 8,192-token window). The rejected message stayed in the history, so every later turn was even longer and failed too. The session was stuck until restart.
- **Alternatives considered**: Keeping the failed message so the user can "retry" (it can't succeed unchanged); trimming old messages automatically (that's context budgeting, a bigger feature for later).

## 2026-10-04: The context window is the server's setting; Adham explains overflows and offers /clear

- **Decision**: Adham doesn't try to size the context. When the server reports `context_length_exceeded`, the CLI explains it and suggests `/clear` or raising Context Length in Studio. `/clear` keeps the system prompt and drops the conversation.
- **Why**: Over the OpenAI-compatible API there's no context-size parameter; Studio fixes it at model load. (By contrast, Ollama accepts `num_ctx` per request and silently truncates old tokens when a prompt is over the limit: no error, but the model quietly loses the start of the conversation.)
- **Alternatives considered**: Silent truncation like Ollama (hides the problem); automatic compaction or a sliding window over tool results (planned for later; needs a token budget setting).

## 2026-10-04: Diagnose "I had to type continue" before fixing it

- **Decision**: Each turn records how it ended (`TurnOutcome`: finish reason, tool-call count, whether it ended with text after the last tool result). The CLI prints a grey note when a turn ends without an answer: cut off at the length limit, empty response, or silent after a tool call.
- **Why**: An agent on a local model can stop early for at least three different reasons, and each needs a different fix: replies cut off at the output limit (an automatic continuation nudge), completely empty responses (a retry), and the model going quiet or announcing an action without calling the tool (a different nudge). Guessing would mean shipping the wrong fix.
- **Alternatives considered**: Adding the continuation and retry fixes up front (they hide the symptom, and neither handles the third cause); always sending a hidden "continue" (makes the model ramble when it was actually done).

## 2026-10-04: Nudge once when the model goes silent after a tool call

- **Decision**: If a turn ran at least one tool and then ended (`finish: stop`) without any text after the last tool result, `AgentSession` adds one hidden user message ("You ran tools but haven't answered yet. Using the tool results above, answer my previous request now.") and asks again. At most once per turn. The nudge is removed from the history afterwards, and the CLI shows a grey note when it happened.
- **Why**: Seen live and intermittently on Qwen3.6-35B-A3B: `Glob` ran, then the turn ended with nothing, and the user had to type "continue". The turn-outcome note identified the cause as "silent after a tool call", not a length cut-off or an empty response.
- **Alternatives considered**: Several nudges (a model that keeps going quiet should be reported, not pushed indefinitely); keeping the nudge in history (later turns would see an instruction the user never wrote); a stronger system prompt alone (not reliable for an intermittent behaviour).

## 2026-10-06: Read before write

- **Decision**: `Edit` only works on a file the model has read this session (a partial read is enough, because `old_string` pins the change to text it has seen). `Write` over an existing file needs a full read. Both refuse if the file's modification time or size changed since the read. `FileReadTracker` keeps the fingerprints; our own writes refresh them, but an `Edit` after a partial read never turns into a "full read".
- **Why**: A model that edits a file it hasn't looked at (or that changed underneath it) produces confident, wrong changes. Refusal messages spell out the recovery ("Read '<path>' with no offset/limit, then call Write again") so the model fixes the situation instead of improvising.
- **Alternatives considered**: No check (fast, unsafe); hashing file content (exact, but costs a full read of every file on every check; modification time plus size is enough in practice).

## 2026-10-06: Edit is exact text replacement with recovery hints

- **Decision**: `Edit(file_path, old_string, new_string, replace_all?)` replaces exact text. `old_string` must match once unless `replace_all` is true. When it isn't found, the error shows the closest line (found by the first token of `old_string`) with two lines of context, and the exact `Read` call to re-read that region.
- **Why**: The most common failure is a stale `old_string` after an earlier edit moved lines. A bare "not found" leads to repeated blind retries; showing where the text actually is now breaks that loop.
- **Alternatives considered**: Line-number based edits (break as soon as anything above changes); whole-file rewrites for every change (expensive, and easy to clobber unrelated code); fuzzy matching that applies the edit anyway (silently wrong edits).

## 2026-10-06: The user approves every file change, with a diff

- **Decision**: `Edit` and `Write` compute the change, then call an `IChangeApprover` before touching disk. The CLI's `ConsoleChangeApprover` prints a compact diff (changed lines, two lines of context, `⋮` between distant changes, capped at 60 lines) and asks `[y/N]`. Anything but `y`/`yes` declines; a declined change returns "The user declined… ask the user what they want instead" to the model.
- **Why**: These are the first tools that can change the user's code. A per-change prompt is the simplest safe default until permission rules exist.
- **Alternatives considered**: Ask once per session (one "yes" would unlock every later change); a full permission system with allow/deny rules now (bigger; planned next); approval in the function-invocation middleware (it never sees the diff, only the tool arguments).

## 2026-10-06: Edits keep the file's line endings

- **Decision**: `Edit` matches on LF-normalized text (so an LF `old_string` matches a CRLF file), then writes the result back with CRLF if the file used CRLF.
- **Why**: Normalizing to LF and writing LF would silently convert every line of a Windows file, turning a one-line fix into a whole-file diff.
- **Alternatives considered**: Write LF always (simple, but rewrites line endings); require the model to send matching line endings (models don't).

## 2026-10-06: Glob stays files-only

- **Decision**: `Glob` keeps returning files only. Listing folders will come from the shell tool's read-only commands (`ls`/`dir`), auto-allowed, rather than from a change to `Glob`.
- **Why**: Seen live: in a folder that only contains subfolders, `Glob *` returns "No files found." and the model called the folder empty. Teaching `Glob` about folders would mix two jobs in one tool; a read-only shell listing is the natural place, and the model already knows those commands.
- **Alternatives considered**: `Glob` listing folders with a trailing `/` (works, but blurs the tool's contract); a separate `List` tool (one more tool to describe in a small context window).

