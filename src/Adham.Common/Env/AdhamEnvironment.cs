namespace Adham.Common.Env;

// Reads ADHAM_* settings. The reader is injectable so tests never touch the real environment.
public sealed class AdhamEnvironment
{
    private readonly Func<string, string?> _read;

    public AdhamEnvironment() : this(Environment.GetEnvironmentVariable) { }

    public AdhamEnvironment(Func<string, string?> read) => _read = read;

    // Real environment variables win; the nearest .env file fills in the rest.
    public static AdhamEnvironment FromProcessAndDotEnv(string startDir) =>
        Layered(Environment.GetEnvironmentVariable, DotEnv.Load(DotEnv.Find(startDir)));

    internal static AdhamEnvironment Layered(Func<string, string?> process, IReadOnlyDictionary<string, string> file) =>
        new(name => process(name) is { Length: > 0 } fromProcess ? fromProcess : file.GetValueOrDefault(name));

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
