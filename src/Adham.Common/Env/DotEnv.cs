namespace Adham.Common.Env;

// Local, git-ignored settings (like ADHAM_API_KEY) in a ".env" file: KEY=VALUE per line.
public static class DotEnv
{
    public const string FileName = ".env";

    // The nearest .env in startDir or any parent, so `adham` finds it from any subfolder.
    public static string? Find(string startDir)
    {
        for (var dir = new DirectoryInfo(startDir); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    public static IReadOnlyDictionary<string, string> Load(string? path) =>
        path is null ? new Dictionary<string, string>() : Parse(File.ReadAllLines(path));

    public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
                continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                value = value[1..^1];

            values[key] = value;
        }
        return values;
    }
}
