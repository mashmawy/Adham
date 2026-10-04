namespace Adham.Tools.Abstractions;

// The folder the agent works in. Tools accept paths relative to it, because small models
// handle short relative paths better than long absolute ones.
public sealed record WorkingDirectory(string Path)
{
    public string Resolve(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            trimmed = System.IO.Path.Combine(home, trimmed.TrimStart('~').TrimStart('/', '\\'));
        }
        return System.IO.Path.GetFullPath(trimmed, Path);
    }

    public string Relative(string fullPath) =>
        System.IO.Path.GetRelativePath(Path, fullPath).Replace('\\', '/');
}
