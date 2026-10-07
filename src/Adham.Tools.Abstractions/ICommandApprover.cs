namespace Adham.Tools.Abstractions;

// A shell command a tool wants to run, e.g. Shell = "PowerShell", Command = "dotnet test".
public sealed record CommandRequest(string Shell, string Command);

// Asked before running any command that isn't read-only. The CLI shows the command and asks the user.
public interface ICommandApprover
{
    ValueTask<bool> ApproveAsync(CommandRequest request, CancellationToken cancellationToken);
}
