using Adham.Tools.Abstractions;

namespace Adham.Tools.Files.Tests;

// Stands in for the user: answers every approval with Answer and remembers what it was shown.
internal sealed class ScriptedApprover(bool answer = true) : IChangeApprover
{
    public bool Answer { get; set; } = answer;
    public List<FileChange> Asked { get; } = [];

    public ValueTask<bool> ApproveAsync(FileChange change, CancellationToken cancellationToken)
    {
        Asked.Add(change);
        return ValueTask.FromResult(Answer);
    }
}
