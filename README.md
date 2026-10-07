# Adham

[![ci](https://github.com/mashmawy/Adham/actions/workflows/ci.yml/badge.svg)](https://github.com/mashmawy/Adham/actions/workflows/ci.yml)

A local-first coding agent for local LLMs. .NET 10, OpenAI-compatible backend (Unsloth Studio, llama.cpp, LM Studio, vLLM).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Unsloth Studio](https://unsloth.ai/) with a model loaded and an API key

## Quick start

1. Start Unsloth Studio and load a model.
2. Create an API key in Studio (**Settings → API**); it starts with `sk-unsloth-`.
3. Install Adham as a global tool and save your key once:

```powershell
dotnet pack src/Adham.Cli -c Release
dotnet tool install -g Adham --add-source ./nupkgs --prerelease

New-Item -ItemType Directory -Force ~/.adham | Out-Null
Set-Content ~/.adham/.env "ADHAM_API_KEY=sk-unsloth-..."
```

4. Run it in the folder you want to work on. That folder is Adham's working directory:

```powershell
cd C:\code\my-project
adham
```

To pick up code changes, pack again and reinstall: `dotnet tool uninstall -g Adham`, then the install line above. Without installing, `dotnet run --project src/Adham.Cli` runs it with the current folder as the working directory.

**Where settings come from** (first match wins): environment variables, then the nearest `.env` in the working directory or a parent folder (per-project settings, git-ignored), then `~/.adham/.env` (user-wide; `ADHAM_HOME` moves this folder).

| Variable | Default | Meaning |
|---|---|---|
| `ADHAM_API_KEY` | (required) | API key for the model server |
| `ADHAM_BASE_URL` | `http://localhost:8888/v1` | Any OpenAI-compatible server (Unsloth Studio, llama.cpp, LM Studio, vLLM) |
| `ADHAM_MODEL` | the model the server has loaded | Model id from `GET /v1/models` |

Type `/clear` to start a fresh conversation, `/exit` or Ctrl+C to quit.

**Context window.** Unsloth Studio sets the model's context length when it loads the model (Model settings → Context Length); Adham can't change it over the OpenAI-compatible API. Tool results stay in the conversation, so a small window (e.g. 8K) fills up after a few file reads. Raise it in Studio (32K is a good start) or use `/clear`.

Run it from the folder you want to ask about: that's the agent's working directory. Tool calls appear in grey (`⚙ Glob(pattern: **/*.cs)`) as the model makes them.

## Tools

| Tool | What the model can do with it |
|---|---|
| `Glob` | Find files by pattern (`**/*.cs`), relative to the working directory; skips `bin`, `obj`, `.git`, `node_modules` |
| `Read` | Read a text file with line numbers; `offset`/`limit` for large files; binary files are refused |
| `Edit` | Replace exact text in a file (unique match, or `replace_all`) |
| `Write` | Create a file, or replace a whole file |
| `PowerShell` / `Bash` | Run a command in the working directory: PowerShell on Windows (`pwsh` if installed), bash elsewhere |

### Changing files safely

- **You approve every change.** Before `Edit` or `Write` touches a file, Adham shows a diff and asks `Allow this change? [y/N]`. Anything but `y` is a no, and nothing is written.
- **Read before write.** The model can only edit a file it has read this session, and can only replace a whole file it has read in full. If the file changed on disk since it was read, the change is refused until it's read again.
- **Line endings are kept.** An edit to a CRLF file stays CRLF.
- Writes are atomic (temp file, then swap), so a crash never leaves a half-written file.

### Running commands safely

Every command goes through the same gate before it runs:

1. **Refused**: destructive commands never run and can't be approved: deleting a drive root, your home folder or a system folder; formatting disks; shutdown/restart; `sudo`/run-as-administrator; `git push --force`, `git reset --hard`, `git clean -f`; `DROP TABLE`; `terraform destroy`; `kubectl delete`; piping a download into a shell.
2. **Runs right away**: read-only commands, such as `ls`/`dir`/`Get-ChildItem`, `cat`/`Get-Content`, `grep`, `find` (without `-exec`/`-delete`), and `git status`/`diff`/`log`/`show`/`blame`, plus `branch`/`tag`/`remote` when they only list.
3. **Asks you first**: everything else, including builds and test runs (they execute project code). Adham shows the exact command and asks `Run this command? [y/N]`.

A command only counts as read-only if every part of it is: `ls && rm notes.txt` asks. Anything whose effect can't be read from the text (`$(...)`, backticks, variables, redirects, script blocks, several lines) also asks, and so does anything that touches secrets (`.env`, `.ssh`, `.aws`, ...).

Under each command you see its exit code and the last lines of its output (or why it was refused), so a claim like "all tests pass" is something you can check.

Each call runs in a fresh process in the working directory. The default timeout is 2 minutes (maximum 10), and output over 64 KB per stream is cut.

## License

[MIT](LICENSE)
