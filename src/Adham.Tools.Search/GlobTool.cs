using System.ComponentModel;
using Adham.Tools.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Adham.Tools.Search;

public sealed class GlobTool(WorkingDirectory cwd) : ITool
{
    internal const int MaxResults = 100;

    // Build output and dependencies drown out real results and burn context.
    private static readonly string[] Ignored = ["**/bin/**", "**/obj/**", "**/.git/**", "**/node_modules/**"];

    public string Name => "Glob";

    public string Description =>
        "Find files by path pattern. Returns paths relative to the working directory, newest first.\n" +
        "- pattern: a glob like \"**/*.<ext>\" (every file with that extension, in any folder) or \"src/**/*\". " +
        "\"*.<ext>\" only matches the top folder; use \"**/\" to include subfolders. " +
        "Don't know what kind of project it is yet? Start with \"**/*\".\n" +
        "- path (optional): folder to search in; defaults to the working directory.\n" +
        $"Returns at most {MaxResults} paths. Skips bin, obj, .git and node_modules.";

    public bool IsReadOnly => true;

    public AIFunction AsAIFunction() =>
        ToolFunction.Create(Execute, Name, Description);

    internal string Execute(
        [Description("Glob pattern, e.g. \"**/*\" or \"**/*.<ext>\".")] string pattern,
        [Description("Folder to search in. Omit for the working directory.")] string? path = null)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return "Error: pattern is required.";

        var root = string.IsNullOrWhiteSpace(path) ? cwd.Path : cwd.Resolve(path);
        if (!Directory.Exists(root))
            return $"Error: folder not found: {path}";

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(pattern);
        matcher.AddExcludePatterns(Ignored);

        var files = matcher.GetResultsInFullPath(root)
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => cwd.Relative(f.FullName))
            .ToList();

        if (files.Count == 0)
            return "No files found.";

        var shown = string.Join('\n', files.Take(MaxResults));
        return files.Count > MaxResults
            ? $"{shown}\n<{files.Count - MaxResults} more not shown; narrow the pattern>"
            : shown;
    }
}
