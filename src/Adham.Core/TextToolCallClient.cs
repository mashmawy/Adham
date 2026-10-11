using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;

namespace Adham.Core;

// Sits between the tool loop and the server. Text from "<tool_call>" on is held back; when the reply
// ends, a readable call becomes a real one, so the tool loop runs it (with the usual approval) instead
// of the turn ending on it. Anything it can't read is shown as it came.
internal sealed class TextToolCallClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pending = "";            // could still be the start of "<tool_call>"
        StringBuilder? held = null;  // everything from "<tool_call>" on
        var realCall = false;
        ChatResponseUpdate? last = null;

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            last = update;
            realCall |= update.Contents.OfType<FunctionCallContent>().Any();
            if (!update.Contents.OfType<TextContent>().Any())
            {
                yield return update;
                continue;
            }

            var text = string.Concat(update.Contents.OfType<TextContent>().Select(t => t.Text));
            string shown;
            if (held is not null)
            {
                held.Append(text);
                shown = "";
            }
            else
            {
                pending += text;
                var at = pending.IndexOf(TextToolCalls.Open, StringComparison.Ordinal);
                if (at >= 0)
                {
                    shown = pending[..at];
                    held = new StringBuilder(pending[at..]);
                    pending = "";
                }
                else
                {
                    var keep = OpenTagPrefix(pending);
                    shown = pending[..^keep];
                    pending = pending[^keep..];
                }
            }
            yield return WithText(update, shown);
        }

        if (held is null)
        {
            if (pending.Length > 0)
                yield return Like(last, [new TextContent(pending)]);
            yield break;
        }

        // A reply with a real call too: the text is noise around it, not a call to run twice.
        var calls = realCall ? null : TextToolCalls.Parse(held.ToString(), options?.Tools);
        if (calls is null)
        {
            yield return Like(last, [new TextContent(held.ToString())]);
            yield break;
        }

        var update2 = Like(last, [.. calls]);
        update2.FinishReason = ChatFinishReason.ToolCalls;
        yield return update2;
    }

    // How much of the end of the text could be the start of "<tool_call>", split across chunks.
    private static int OpenTagPrefix(string text)
    {
        for (var length = Math.Min(text.Length, TextToolCalls.Open.Length - 1); length > 0; length--)
            if (text.EndsWith(TextToolCalls.Open[..length], StringComparison.Ordinal))
                return length;
        return 0;
    }

    private static ChatResponseUpdate WithText(ChatResponseUpdate update, string shown)
    {
        var contents = update.Contents.Where(c => c is not TextContent).ToList();
        if (shown.Length > 0)
            contents.Insert(0, new TextContent(shown));
        var copy = Like(update, contents);
        copy.FinishReason = update.FinishReason;
        return copy;
    }

    // Same message as the reply it belongs to, so its text and calls end up in one assistant message.
    private static ChatResponseUpdate Like(ChatResponseUpdate? update, IList<AIContent> contents) =>
        new(update?.Role ?? ChatRole.Assistant, contents)
        {
            MessageId = update?.MessageId,
            ResponseId = update?.ResponseId,
            ModelId = update?.ModelId,
            CreatedAt = update?.CreatedAt,
        };
}
