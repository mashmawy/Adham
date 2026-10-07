namespace Adham.Tools.Shell;

// Commands that only look at things, so they run without asking. A line qualifies only if it has
// nothing risky (see CommandSplitter) and every segment is on the list below.
public static class ReadOnlyRules
{
    public static bool IsReadOnly(SplitCommand split, ShellKind kind) =>
        !split.IsRisky
        && split.Segments.Count > 0
        && split.Segments.All(s => !TouchesSecrets(s) && IsReadOnlySegment(s, kind));

    private static bool IsReadOnlySegment(CommandSegment segment, ShellKind kind)
    {
        var name = segment.Name.ToLowerInvariant();
        var args = segment.Words.Skip(1).ToArray();

        if (name == "git")
            return IsReadOnlyGit(args);

        return kind == ShellKind.PowerShell ? IsReadOnlyPowerShell(name) : IsReadOnlyBash(name, args);
    }

    private static readonly HashSet<string> SimpleBash =
    [
        "ls", "cat", "head", "tail", "wc", "stat", "file", "diff", "cut", "tr", "which", "du", "df",
        "pwd", "whoami", "echo", "basename", "dirname", "realpath", "grep", "egrep", "fgrep",
    ];

    private static bool IsReadOnlyBash(string name, string[] args) => name switch
    {
        _ when SimpleBash.Contains(name) => true,
        // ripgrep's --pre runs a program on every file.
        "rg" => !args.Any(a => a.StartsWith("--pre", StringComparison.Ordinal)),
        // find can run commands or delete files.
        "find" => !args.Any(a => a is "-exec" or "-execdir" or "-ok" or "-okdir" or "-delete"
                                  || a.StartsWith("-fprint", StringComparison.Ordinal) || a == "-fls"),
        // sort -o writes a file; uniq IN OUT writes OUT.
        "sort" => !args.Any(a => a is "-o" || a.StartsWith("--output", StringComparison.Ordinal)),
        "uniq" => args.Count(a => !a.StartsWith('-')) <= 1,
        _ => false,
    };

    // Read-only cmdlets and their common aliases. Arguments are literal here: the splitter already
    // ruled out variables, subexpressions, script blocks and redirects.
    private static readonly HashSet<string> ReadOnlyPowerShellCommands =
    [
        "get-childitem", "ls", "dir", "gci",
        "get-content", "cat", "gc", "type",
        "get-item", "gi", "test-path", "resolve-path", "rvpa", "get-location", "pwd", "gl",
        "select-string", "sls", "measure-object", "measure", "select-object", "select",
        "sort-object", "sort", "format-table", "ft", "format-list", "fl", "out-string",
        "get-command", "gcm", "get-filehash", "compare-object", "compare",
        "write-output", "echo", "write", "split-path", "join-path", "get-date",
        "whoami", "hostname", "where.exe",
    ];

    private static bool IsReadOnlyPowerShell(string name) => ReadOnlyPowerShellCommands.Contains(name);

    // Subcommands that only read the repository.
    private static readonly HashSet<string> ReadOnlyGit =
    [
        "status", "diff", "log", "show", "blame", "rev-parse", "ls-files", "ls-tree", "describe",
        "shortlog", "grep", "show-ref", "cat-file", "branch", "tag", "remote", "stash", "reflog",
    ];

    // Global options that change which repo/config/program git uses, or write output files.
    private static bool IsUnsafeGitFlag(string flag) =>
        flag is "-c" or "-C" || flag.StartsWith("--git-dir", StringComparison.Ordinal) || flag.StartsWith("--work-tree", StringComparison.Ordinal)
        || flag.StartsWith("--exec-path", StringComparison.Ordinal) || flag.StartsWith("--config-env", StringComparison.Ordinal)
        || flag.StartsWith("--output", StringComparison.Ordinal) || flag is "--ext-diff" or "--textconv";

    private static bool IsReadOnlyGit(string[] args)
    {
        if (args.Length == 0 || args.Any(IsUnsafeGitFlag))
            return false;

        var sub = args[0];
        var rest = args.Skip(1).ToArray();
        var positionals = rest.Where(a => !a.StartsWith('-')).ToArray();
        if (!ReadOnlyGit.Contains(sub))
            return false;

        return sub switch
        {
            // Listing only: a name creates (or with -d/-m deletes/renames) a branch or tag.
            "branch" => positionals.Length == 0 && rest.All(f => f is "-a" or "-r" or "-v" or "-vv" or "-l" or "--list" or "--all" or "--remotes" or "--show-current" or "--merged" or "--no-merged"),
            "tag" => positionals.Length == 0 || rest.Contains("-l") || rest.Contains("--list"),
            "remote" => positionals.Length == 0 || (positionals is ["show", _] or ["get-url", _]),
            "stash" => rest is ["list"] || (rest.Length >= 1 && rest[0] == "show"),
            "reflog" => rest.Length == 0 || rest[0] == "show",
            _ => true,
        };
    }

    // Arguments pointing at secrets always need a human: a read-only "cat .env" would hand the API key to the model.
    private static readonly string[] SecretMarkers = [".env", ".ssh", ".aws", ".gnupg", "id_rsa", "id_ed25519", ".netrc", ".git-credentials", "secrets.json"];

    private static bool TouchesSecrets(CommandSegment segment) =>
        segment.Words.Skip(1).Any(w =>
        {
            var normalized = w.Replace('\\', '/').ToLowerInvariant();
            return SecretMarkers.Any(m => normalized == m || normalized.EndsWith("/" + m, StringComparison.Ordinal)
                                          || normalized.Contains("/" + m + "/", StringComparison.Ordinal) || normalized.StartsWith(m + "/", StringComparison.Ordinal));
        });
}
