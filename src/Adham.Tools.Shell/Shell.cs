namespace Adham.Tools.Shell;

public enum ShellKind
{
    PowerShell,
    Bash,
}

// Which shell to run and how. PowerShell on Windows (pwsh 7+ if installed, which supports && and ||;
// otherwise the built-in powershell.exe), bash elsewhere (sh if bash is missing).
public sealed record Shell(ShellKind Kind, string Executable)
{
    public static Shell ForThisMachine() =>
        OperatingSystem.IsWindows()
            ? new Shell(ShellKind.PowerShell, FindOnPath("pwsh") ?? "powershell.exe")
            : new Shell(ShellKind.Bash, File.Exists("/bin/bash") ? "/bin/bash" : FindOnPath("bash") ?? "/bin/sh");

    // What the result echoes before the command, like a terminal prompt.
    public string Prompt => Kind == ShellKind.PowerShell ? "PS>" : "$";

    public IReadOnlyList<string> Arguments(string command) =>
        Kind == ShellKind.PowerShell
            // Without the encoding line, Windows PowerShell writes non-ASCII output in the console code page.
            ? ["-NoProfile", "-NonInteractive", "-Command", "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " + command]
            // Plain -c, not a login shell: profiles stay out of the agent's commands.
            : ["-c", command];

    internal static string? FindOnPath(string name)
    {
        var names = OperatingSystem.IsWindows() ? new[] { name + ".exe", name } : new[] { name };
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var candidate in names)
            {
                var full = Path.Combine(dir.Trim(), candidate);
                if (File.Exists(full))
                    return full;
            }
        return null;
    }
}
