using System.Text.RegularExpressions;

namespace Adham.Tools.Shell;

// Commands that are refused outright: no prompt, no way to approve them.
// They destroy data or the machine, escalate privileges, or run code downloaded on the fly.
public static partial class DenyRules
{
    public static string? Check(string command, SplitCommand split, ShellKind kind)
    {
        if (ForkBomb().IsMatch(command))
            return "a fork bomb";
        if (DropSql().IsMatch(command))
            return "dropping a database or table";

        for (var i = 0; i < split.Segments.Count; i++)
        {
            var segment = split.Segments[i];
            var name = Normalize(segment.Name);
            var args = segment.Words.Skip(1).ToArray();

            if (i > 0 && IsDownloader(Normalize(split.Segments[i - 1].Name)) && RunsCode(name))
                return "running code downloaded on the fly (download piped into a shell)";

            if (GitDenial(name, args) is { } git)
                return git;

            if (name == "terraform" && args.FirstOrDefault() == "destroy")
                return "terraform destroy";
            if (name == "kubectl" && args.FirstOrDefault() == "delete")
                return "kubectl delete";

            var reason = kind == ShellKind.PowerShell ? PowerShellDenial(name, args) : BashDenial(name, args);
            if (reason is not null)
                return reason;
        }
        return null;
    }

    private static string? BashDenial(string name, string[] args)
    {
        switch (name)
        {
            case "sudo" or "su" or "doas":
                return $"privilege escalation ({name})";
            case "dd" or "fdisk" or "parted" or "shred" or "wipefs" or "insmod" or "modprobe":
                return $"a disk or system tool ({name})";
            case "shutdown" or "reboot" or "halt" or "poweroff":
                return $"shutting down or restarting the machine ({name})";
            case "init" when args.FirstOrDefault() is "0" or "6":
                return "shutting down or restarting the machine (init)";
            case "rm" or "rmdir" when args.FirstOrDefault(a => !a.StartsWith('-') && IsDangerousBashPath(a)) is { } target:
                return $"deleting {target}";
        }
        return name.StartsWith("mkfs", StringComparison.Ordinal) ? $"formatting a disk ({name})" : null;
    }

    private static string? PowerShellDenial(string name, string[] args)
    {
        switch (name)
        {
            case "format-volume" or "clear-disk" or "initialize-disk" or "remove-partition" or "format" or "diskpart":
                return $"formatting or wiping a disk ({name})";
            case "stop-computer" or "restart-computer" or "shutdown":
                return $"shutting down or restarting the machine ({name})";
            case "bcdedit":
                return "changing the boot configuration";
            case "start-process" when args.Any(a => a.Equals("runas", StringComparison.OrdinalIgnoreCase)):
                return "running a process as administrator (-Verb RunAs)";
            case "remove-item" or "rm" or "del" or "erase" or "rd" or "rmdir" or "ri"
                when args.FirstOrDefault(a => !a.StartsWith('-') && IsDangerousWindowsPath(a)) is { } target:
                return $"deleting {target}";
        }
        return null;
    }

    private static string? GitDenial(string name, string[] args)
    {
        if (name != "git" || args.Length == 0)
            return null;
        var sub = args[0];
        var flags = args.Skip(1).ToArray();
        return sub switch
        {
            "push" when flags.Any(f => f is "--force" or "-f" || f.StartsWith('+') || IsShortFlagWith(f, 'f')) =>
                "git push --force (rewrites shared history)",
            "reset" when flags.Contains("--hard") => "git reset --hard (throws away uncommitted work)",
            "clean" when flags.Any(f => f == "--force" || IsShortFlagWith(f, 'f')) => "git clean -f (deletes untracked files)",
            _ => null,
        };
    }

    private static bool IsShortFlagWith(string flag, char c) =>
        flag.Length > 1 && flag[0] == '-' && flag[1] != '-' && flag.Contains(c, StringComparison.Ordinal);

    private static bool IsDownloader(string name) =>
        name is "curl" or "wget" or "iwr" or "irm" or "invoke-webrequest" or "invoke-restmethod";

    private static bool RunsCode(string name) =>
        name is "sh" or "bash" or "zsh" or "dash" or "python" or "python3" or "node" or "iex" or "invoke-expression" or "pwsh" or "powershell";

    // "/", "/*", "~", "~/", "$HOME", "*", ".", "..", or a top-level folder such as /etc or /usr.
    private static bool IsDangerousBashPath(string path)
    {
        var p = path.TrimEnd('/');
        return p is "" or "/*" or "~" or "~/*" or "*" or "." or ".." or "./*"
            || p.StartsWith("$HOME", StringComparison.Ordinal) && p.TrimEnd('*', '/') == "$HOME"
            || TopLevelFolder().IsMatch(p);
    }

    // A drive root, the home folder, or a system folder.
    private static bool IsDangerousWindowsPath(string path)
    {
        var p = path.Trim('"', '\'').Replace('/', '\\').TrimEnd('\\', '*').TrimEnd('\\');
        if (p is "" or "~" or "." or ".." || p.StartsWith("$env:USERPROFILE", StringComparison.OrdinalIgnoreCase) && p.Length <= "$env:USERPROFILE".Length)
            return true;
        if (DriveRoot().IsMatch(p))
            return true;
        var lower = p.ToLowerInvariant();
        string[] system = [@"c:\windows", @"c:\program files", @"c:\program files (x86)", @"c:\users", @"c:\programdata"];
        return system.Contains(lower) || UserProfileRoot().IsMatch(lower);
    }

    private static string Normalize(string name)
    {
        var n = name.ToLowerInvariant().Replace('\\', '/');
        n = n[(n.LastIndexOf('/') + 1)..];
        return n.EndsWith(".exe", StringComparison.Ordinal) ? n[..^4] : n;
    }

    [GeneratedRegex(@":\(\)\s*\{\s*:\s*\|\s*:\s*&\s*\}\s*;\s*:")]
    private static partial Regex ForkBomb();

    [GeneratedRegex(@"\bDROP\s+(TABLE|DATABASE|SCHEMA)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DropSql();

    [GeneratedRegex(@"^/[^/]+$")]
    private static partial Regex TopLevelFolder();

    [GeneratedRegex(@"^[a-zA-Z]:$")]
    private static partial Regex DriveRoot();

    [GeneratedRegex(@"^c:\\users\\[^\\]+$")]
    private static partial Regex UserProfileRoot();
}
