using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using StreamingChatCompletionUpdate = OpenAI.Chat.StreamingChatCompletionUpdate;

namespace Adham.Core;

// Inside the tool loop, so each model call of a turn is timed on its own and reported when it ends.
internal sealed class TimedChatClient(IChatClient inner, Action<ModelCallTiming> report) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        var response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        report(new ModelCallTiming(clock.Elapsed, null, response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount,
            ServerTimingsOf(response.RawRepresentation)));
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        TimeSpan? firstToken = null;
        UsageDetails? usage = null;
        ServerTimings? server = null;

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            // Thinking counts as the first token: the server has read the prompt by then.
            if (firstToken is null && update.Contents.Any(c => c is not UsageContent))
                firstToken = clock.Elapsed;
            foreach (var u in update.Contents.OfType<UsageContent>())
                usage = u.Details;
            // The timings come with the usage, in the last chunk; only read the raw JSON there.
            if (update.RawRepresentation is StreamingChatCompletionUpdate { Usage: not null } raw)
                server = ServerTimingsOf(raw) ?? server;

            yield return update;
        }

        report(new ModelCallTiming(clock.Elapsed, firstToken, usage?.InputTokenCount, usage?.OutputTokenCount, server));
    }

    // The SDK keeps fields it doesn't know (like "timings") and writes them back out with the rest.
    private static ServerTimings? ServerTimingsOf(object? raw)
    {
        if (raw is null)
            return null;
        try
        {
            return ServerTimings.Parse(ModelReaderWriter.Write(raw).ToString());
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}
