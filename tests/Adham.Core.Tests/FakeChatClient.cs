using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Adham.Core.Tests;

// A scripted model: tests decide what it streams back for a given conversation. No GPU needed.
public sealed class FakeChatClient(Func<IEnumerable<ChatMessage>, IAsyncEnumerable<ChatResponseUpdate>> respond) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Adham streams; use GetStreamingResponseAsync.");

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => respond(messages);

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    public static async IAsyncEnumerable<ChatResponseUpdate> StreamText(
        string text, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var chunk in text.Chunk(8))
        {
            ct.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, new string(chunk));
            await Task.Yield();
        }
    }

    public static async IAsyncEnumerable<ChatResponseUpdate> StreamToolCall(
        string callId, string name, Dictionary<string, object?> args)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(callId, name, args)]);
        await Task.Yield();
    }
}
