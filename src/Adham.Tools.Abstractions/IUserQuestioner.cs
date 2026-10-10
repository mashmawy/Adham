namespace Adham.Tools.Abstractions;

// Asks the user a freeform question and returns their answer. With options, the user can
// reply with an option number (1-based) or type the full answer.
public interface IUserQuestioner
{
    ValueTask<string> AskUserAsync(string question, string[]? options, CancellationToken cancellationToken);
}
