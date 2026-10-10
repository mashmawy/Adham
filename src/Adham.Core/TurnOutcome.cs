using Microsoft.Extensions.AI;

namespace Adham.Core;

// How a completed turn ended. Local models sometimes stop without a real answer
// (cut off, empty, or silent after a tool call); this says which, so it can be fixed.
public sealed record TurnOutcome(ChatFinishReason? FinishReason, int ToolCalls, bool EndedWithText, int Nudges = 0, bool ContextFull = false)
{
    // A short explanation when the turn didn't end with a plain answer; null when it did.
    // A full context comes first: then an empty or cut-off reply is a symptom, not the model's choice.
    public string? Note =>
        ContextFull && (!EndedWithText || FinishReason == ChatFinishReason.Length)
            ? "the context window is full, so the model can't answer any more: type /clear to start over"
        : FinishReason == ChatFinishReason.Length ? "stopped: the reply hit the output length limit"
        : FinishReason == ChatFinishReason.ContentFilter ? "stopped: the server's content filter"
        : EndedWithText && Nudges > 0 ? "the model went quiet after a tool call; Adham nudged it once to answer"
        : EndedWithText ? null
        : ToolCalls > 0 ? $"the model ended its turn without answering after the tool call{(Nudges > 0 ? ", even after a nudge" : "")} (finish: {Describe(FinishReason)})"
        : $"empty response: the model returned nothing (finish: {Describe(FinishReason)})";

    private static string Describe(ChatFinishReason? reason) => reason?.Value ?? "none";

    internal sealed class Tracker
    {
        private ChatFinishReason? _finishReason;
        private int _toolCalls;
        private bool _textSinceLastTool;
        private int _nudges;

        public void Observe(ChatResponseUpdate update)
        {
            if (update.FinishReason is { } reason)
                _finishReason = reason;

            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent:
                        _toolCalls++;
                        _textSinceLastTool = false;
                        break;
                    case FunctionResultContent:
                        _textSinceLastTool = false;
                        break;
                    case TextContent { Text.Length: > 0 } text when !string.IsNullOrWhiteSpace(text.Text):
                        _textSinceLastTool = true;
                        break;
                }
            }
        }

        public void Nudged() => _nudges++;

        public TurnOutcome Result => new(_finishReason, _toolCalls, _textSinceLastTool, _nudges);
    }
}
