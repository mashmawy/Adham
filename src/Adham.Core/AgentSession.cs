using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Adham.Core;

// The model is stateless: every turn re-sends the whole conversation. This list *is* the memory.
public sealed class AgentSession
{
    private readonly IChatClient _client;
    private readonly ChatOptions _options;
    private readonly List<ChatMessage> _history = [];

    // Sent (once, and never kept in history) when the model runs a tool and then ends its turn without a word.
    public const string AnswerNudge =
        "You ran tools but haven't answered yet. Using the tool results above, answer my previous request now.";

    public AgentSession(IChatClient client, ChatOptions options, string? systemPrompt = null)
    {
        _client = client;
        _options = options;
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            _history.Add(new ChatMessage(ChatRole.System, systemPrompt));
    }

    public IReadOnlyList<ChatMessage> History => _history;

    // How the most recent completed turn ended (null before the first one).
    public TurnOutcome? LastTurn { get; private set; }

    // Start over: keep the system prompt, forget the conversation (and everything the tools returned).
    public void Clear()
    {
        var keep = _history.Count > 0 && _history[0].Role == ChatRole.System ? 1 : 0;
        _history.RemoveRange(keep, _history.Count - keep);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> SendAsync(
        string text,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var turnStart = _history.Count;
        _history.Add(new ChatMessage(ChatRole.User, text));

        var completed = false;
        try
        {
            // With tools, one turn can be several model calls:
            // assistant asks for a tool → tool result → assistant answers.
            var outcome = new TurnOutcome.Tracker();
            while (true)
            {
                var updates = new List<ChatResponseUpdate>();
                await foreach (var update in _client.GetStreamingResponseAsync(_history, _options, ct).ConfigureAwait(false))
                {
                    updates.Add(update);
                    outcome.Observe(update);
                    yield return update;
                }

                // Keep the whole exchange, tool calls and results included, so the next turn
                // knows what the model already looked at.
                _history.AddMessages(updates);

                // Seen live: the model runs a tool, gets the result, then stops (finish: stop) without a word.
                // Nudge it once to answer, and drop the nudge afterwards so the history reads naturally.
                var soFar = outcome.Result;
                if (soFar is { ToolCalls: > 0, EndedWithText: false, Nudges: 0 } && soFar.FinishReason != ChatFinishReason.Length)
                {
                    _history.Add(new ChatMessage(ChatRole.User, AnswerNudge));
                    outcome.Nudged();
                    continue;
                }
                break;
            }

            _history.RemoveAll(m => m.Role == ChatRole.User && m.Text == AnswerNudge);
            LastTurn = outcome.Result;
            completed = true;
        }
        finally
        {
            // A failed or cancelled turn leaves no trace. Otherwise a message the server rejected
            // (e.g. context_length_exceeded) stays in the history and every later turn fails too.
            if (!completed)
                _history.RemoveRange(turnStart, _history.Count - turnStart);
        }
    }
}
