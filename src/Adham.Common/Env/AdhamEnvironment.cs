using Adham.Common.Paths;

namespace Adham.Common.Env;

// Reads ADHAM_* settings. The reader is injectable so tests never touch the real environment.
public sealed class AdhamEnvironment
{
    private readonly Func<string, string?> _read;

    public AdhamEnvironment() : this(Environment.GetEnvironmentVariable) { }

    public AdhamEnvironment(Func<string, string?> read) => _read = read;

    // First match wins: real environment variables, then the nearest .env above the working
    // directory (per-project settings), then ~/.adham/.env (user-wide, so `adham` works in any folder).
    public static AdhamEnvironment FromProcessAndDotEnv(string startDir) =>
        Layered(
            Environment.GetEnvironmentVariable,
            DotEnv.Load(DotEnv.Find(startDir)),
            DotEnv.Load(File.Exists(AdhamPaths.UserDotEnv()) ? AdhamPaths.UserDotEnv() : null));

    internal static AdhamEnvironment Layered(Func<string, string?> process, params IReadOnlyDictionary<string, string>[] files) =>
        new(name =>
        {
            if (process(name) is { Length: > 0 } fromProcess)
                return fromProcess;
            foreach (var file in files)
                if (file.TryGetValue(name, out var value) && value.Length > 0)
                    return value;
            return null;
        });

    // OpenAI-compatible base URL, including /v1. Default: Unsloth Studio on its default port.
    public Uri BaseUrl
    {
        get
        {
            var raw = _read("ADHAM_BASE_URL");
            return string.IsNullOrWhiteSpace(raw)
                ? new Uri("http://localhost:8888/v1")
                : new Uri(raw);
        }
    }

    public string? ApiKey => _read("ADHAM_API_KEY");

    // Optional. When unset, the CLI uses whatever model the server has loaded.
    public string? Model => _read("ADHAM_MODEL");
}
