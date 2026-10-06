namespace Adham.Tools.Abstractions;

// A change a tool wants to make to a file: the content before (null for a new file) and after.
public sealed record FileChange(string Path, string? Before, string After);

// Asked before any tool changes a file. The CLI shows a diff and asks the user;
// a declined change is never written.
public interface IChangeApprover
{
    ValueTask<bool> ApproveAsync(FileChange change, CancellationToken cancellationToken);
}
