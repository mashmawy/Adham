using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;

namespace Adham.Core;

// The model is stateless: every turn re-sends the whole conversation. This list *is* the memory.
public sealed class AgentSession
{
    private readonly IChatClient _client;
    private readonly ChatOptions _options;
    private readonly List<ChatMessage> _history = [];

    public AgentSession(IChatClient client, ChatOptions options, string? systemPrompt = null)
    {
        _client = client;
        _options = options;
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            _history.Add(new ChatMessage(ChatRole.System, systemPrompt));
    }

    public IReadOnlyList<ChatMessage> History => _history;

    public async IAsyncEnumerable<ChatResponseUpdate> SendAsync(
        string text,
        [EnumeratorCancellation] CancellationToken ct)
    {
        _history.Add(new ChatMessage(ChatRole.User, text));

        var reply = new StringBuilder();
        await foreach (var update in _client.GetStreamingResponseAsync(_history, _options, ct).ConfigureAwait(false))
        {
            reply.Append(update.Text);
            yield return update;
        }

        if (reply.Length > 0)
            _history.Add(new ChatMessage(ChatRole.Assistant, reply.ToString()));
    }
}
