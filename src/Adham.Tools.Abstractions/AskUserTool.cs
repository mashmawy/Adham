using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Adham.Tools.Abstractions;

// Asks the user a freeform question and returns their answer.
public sealed class AskUserTool(IUserQuestioner questioner) : ITool
{
    public string Name => "AskUser";

    public bool IsReadOnly => true;

    public string Description =>
        "Ask the user a question and wait for their response. Use it when you need information " +
        "only the user can provide (e.g., project setup details, preferences, clarification). " +
        "Keep questions specific and concise; the user answers in a single line. The user's answer is returned as a string.";

    public AIFunction AsAIFunction() =>
        ToolFunction.Create(AskAsync, Name, Description);

    internal async Task<string> AskAsync(
        [Description("The question to ask the user. Be specific and concise.")] string question,
        [Description("Optional numbered options. The user can reply with an option number (e.g., '1' or '2') or type the full answer directly. Keep options short.")] string[]? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return "Error: question is required.";

        // Blank options would still get a number on screen.
        var choices = options?.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToArray();
        var answer = await questioner.AskUserAsync(question, choices is { Length: > 0 } ? choices : null, cancellationToken).ConfigureAwait(false);
        return $"User response: {answer}";
    }
}
