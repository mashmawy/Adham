using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Adham.Tools.Abstractions;

// Asks the user a freeform question and returns their answer.
public sealed class AskUserTool(IUserQuestioner approver) : ITool
{
    public string Name => "AskUser";

    public bool IsReadOnly => true;

    public string Description =>
        "Ask the user a question and wait for their response. Use it when you need information " +
        "only the user can provide (e.g., project setup details, preferences, clarification). " +
        "Keep questions specific and concise. The user's answer is returned as a string.";

    public AIFunction AsAIFunction() =>
        ToolFunction.Create(AskAsync, Name, Description);

    internal async Task<string> AskAsync(
        [Description("The question to ask the user. Be specific and concise.")] string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return "Error: question is required.";

        var answer = await approver.AskUserAsync(question, cancellationToken).ConfigureAwait(false);
        return $"User response: {answer}";
    }
}
