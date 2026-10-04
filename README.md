# Adham

[![ci](https://github.com/<owner>/<repo>/actions/workflows/ci.yml/badge.svg)](https://github.com/<owner>/<repo>/actions/workflows/ci.yml)

A local-first coding agent for local LLMs. .NET 10, OpenAI-compatible backend (Unsloth Studio, llama.cpp, LM Studio, vLLM).

## Built in public

Adham is being rebuilt from an earlier prototype, one layer per episode. Every episode ends with something the agent can do.

| # | Episode | What it can do now |
|---|---------|--------------------|
| 01 | First chat | Streams a multi-turn conversation with the model loaded in Unsloth Studio |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Unsloth Studio](https://unsloth.ai/) with a model loaded and an API key

## Quick start

1. Start Unsloth Studio and load a model.
2. Create an API key in Studio (**Settings → API**); it starts with `sk-unsloth-`.
3. Run Adham:

```powershell
$env:ADHAM_API_KEY = "sk-unsloth-..."
dotnet run --project src/Adham.Cli
```

| Variable | Default | Meaning |
|---|---|---|
| `ADHAM_API_KEY` | (required) | API key for the model server |
| `ADHAM_BASE_URL` | `http://localhost:8888/v1` | Any OpenAI-compatible server (Unsloth Studio, llama.cpp, LM Studio, vLLM) |
| `ADHAM_MODEL` | the model the server has loaded | Model id from `GET /v1/models` |

Type `/exit` or press Ctrl+C to quit.

## License

[MIT](LICENSE)
