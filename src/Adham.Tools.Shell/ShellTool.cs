using System.ComponentModel;
using Adham.Tools.Abstractions;
using Microsoft.Extensions.AI;

namespace Adham.Tools.Shell;

// Runs a command in PowerShell (Windows) or bash (elsewhere). Every call goes through the same gate:
// destructive commands are refused, read-only commands run, everything else asks the user first.
public sealed class ShellTool(Shell shell, WorkingDirectory cwd, ICommandApprover approver) : ITool
{
    public string Name => shell.Kind == ShellKind.PowerShell ? "PowerShell" : "Bash";

    public bool IsReadOnly => false;

    public string Description => shell.Kind == ShellKind.PowerShell ? PowerShellDescription : BashDescription;

    private string Common =>
        $"Each call starts a fresh process in the working directory ({cwd.Path}); nothing carries over between calls, " +
        "so don't cd: use paths relative to the working directory.\n" +
        "Use it for builds, tests, git, and anything without a dedicated tool. Don't use it to read, search, list or " +
        "edit files when Read, Glob or Edit can do it.\n" +
        "Read-only commands run immediately; other commands are shown to the user, who approves or declines them; " +
        "destructive commands are refused.\n" +
        "The shell is non-interactive: avoid commands that prompt or open an editor (e.g. git rebase -i).\n" +
        $"Default timeout {ShellRunner.DefaultTimeoutMs / 1000} s, maximum {ShellRunner.MaxTimeoutMs / 1000} s. " +
        $"Output over {ShellRunner.MaxStreamBytes / 1024} KB per stream is cut. Non-zero exit codes are returned, not thrown.";

    private string PowerShellDescription =>
        "Run a PowerShell command and return its exit code and output.\n" + Common + "\n" +
        (Path.GetFileNameWithoutExtension(shell.Executable).Equals("pwsh", StringComparison.OrdinalIgnoreCase)
            ? "This is PowerShell 7: && and || work. Use ; to run commands regardless of the previous result."
            : "This is Windows PowerShell 5.1: && and || do NOT work. Separate commands with ; and check $? if needed.") + "\n" +
        "Use PowerShell syntax, not bash (e.g. Get-ChildItem, not ls -la; $null, not /dev/null). Quote paths that contain spaces.";

    private string BashDescription =>
        "Run a bash command and return its exit code and output.\n" + Common + "\n" +
        "Chain dependent commands with && in one call. Don't separate commands with newlines. Quote paths that contain spaces.";

    public AIFunction AsAIFunction() =>
        ToolFunction.Create(ExecuteAsync, Name, Description);

    internal const string DeclinedMessage =
        "The user declined to run this command. Don't retry it; ask the user what they want instead.";

    internal static string Refused(string reason) =>
        $"Refused: {reason}. This command is blocked and can't be approved. " +
        "Don't try to get the same effect another way; tell the user what you wanted to do and why.";

    internal async Task<string> ExecuteAsync(
        [Description("The command to run.")] string command,
        [Description("Timeout in milliseconds. Omit for the default (120000); maximum 600000.")] int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "Error: command is required.";

        var split = CommandSplitter.Split(command, shell.Kind);
        if (DenyRules.Check(command, split, shell.Kind) is { } reason)
            return Refused(reason);

        if (!ReadOnlyRules.IsReadOnly(split, shell.Kind)
            && !await approver.ApproveAsync(new CommandRequest(Name, command), cancellationToken).ConfigureAwait(false))
            return DeclinedMessage;

        var result = await ShellRunner.RunAsync(shell, command, cwd.Path, ShellRunner.ClampTimeout(timeout), cancellationToken).ConfigureAwait(false);
        return ShellResultText.Format(shell, command, result);
    }
}
