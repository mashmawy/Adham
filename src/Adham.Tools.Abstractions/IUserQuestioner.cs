namespace Adham.Tools.Abstractions;

// Asks the user a freeform question and returns their answer.
public interface IUserQuestioner
{
    ValueTask<string> AskUserAsync(string question, CancellationToken cancellationToken);
}
