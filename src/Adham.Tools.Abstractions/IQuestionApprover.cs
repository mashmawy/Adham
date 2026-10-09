namespace Adham.Tools.Abstractions;

// Asked before returning a freeform user answer to the model.
public interface IQuestionApprover
{
    ValueTask<string> AskUserAsync(string question, CancellationToken cancellationToken);
}
