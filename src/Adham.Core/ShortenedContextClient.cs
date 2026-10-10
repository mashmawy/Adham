using Microsoft.Extensions.AI;

namespace Adham.Core;

// Sits between the tool loop and the server, so every model call (also the ones inside a long turn)
// gets a copy of the conversation with old tool output shortened. The session keeps the full history.
internal sealed class ShortenedContextClient(IChatClient inner, int keepChars) : DelegatingChatClient(inner)
{
    // Tool output kept whole: about 20% of the window (at about 4 characters per token), so a turn
    // that reads a file or two still fits next to the system prompt, the tool schemas and the replies.
    public static int KeepCharsFor(int? contextWindow) => (contextWindow ?? DefaultWindow) * 4 / 5;

    // Unsloth Studio's usual setting, for servers that don't report their window.
    private const int DefaultWindow = 32_768;

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(OldToolResults.Shorten(messages.ToList(), keepChars), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(OldToolResults.Shorten(messages.ToList(), keepChars), options, cancellationToken);
}
