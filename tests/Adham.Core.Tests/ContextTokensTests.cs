using System.Runtime.CompilerServices;
using Adham.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Core.Tests;

// How full the context is, by the server's own count. Unsloth Studio doesn't fail when the context
// is full; it returns an empty or broken reply, so this count is the only warning there is.
public class ContextTokensTests
{
    private static AgentSession Session(
        Func<IEnumerable<ChatMessage>, IAsyncEnumerable<ChatResponseUpdate>> model, int? contextWindow = null)
    {
        var tool = AIFunctionFactory.Create(() => "README.md", "Glob");
        var client = new ChatClientBuilder(new FakeChatClient(model)).UseFunctionInvocation().Build();
        return new AgentSession(client, new ChatOptions { Tools = [tool] }, systemPrompt: "sys", contextWindow);
    }

    // Call 1 asks for Glob (with this much context in use); call 2 sees the result and says nothing.
    private static Func<IEnumerable<ChatMessage>, IAsyncEnumerable<ChatResponseUpdate>> SilentAfterTool(long prompt, List<string> lastMessages) =>
        messages =>
        {
            lastMessages.Add(messages.Last().Text);
            return messages.Last().Role == ChatRole.User && messages.Last().Text != AgentSession.AnswerNudge
                ? WithUsage(FakeChatClient.StreamToolCall("call-1", "Glob", []), prompt, completion: 10)
                : FakeChatClient.StreamTextEndingWith("", ChatFinishReason.ToolCalls);
        };

    [Fact]
    public async Task FullContext_EmptyReply_IsNotNudged_AndSaysTheContextIsFull()
    {
        var sent = new List<string>();
        var session = Session(SilentAfterTool(prompt: 1800, sent), contextWindow: 2000);

        await Send(session, "list files");

        sent.Should().NotContain(AgentSession.AnswerNudge);
        session.LastTurn!.ContextFull.Should().BeTrue();
        session.LastTurn.Note.Should().Contain("context window is full").And.Contain("/clear");
    }

    [Fact]
    public async Task RoomLeft_EmptyReply_IsStillNudged()
    {
        var sent = new List<string>();
        var session = Session(SilentAfterTool(prompt: 500, sent), contextWindow: 2000);

        await Send(session, "list files");

        sent.Should().Contain(AgentSession.AnswerNudge);
        session.LastTurn!.Note.Should().NotContain("context window is full");
    }

    [Fact]
    public async Task FullContext_WithAnAnswer_HasNoNote()
    {
        var session = Session(_ => WithUsage(FakeChatClient.StreamText("done"), prompt: 1900, completion: 20), contextWindow: 2000);

        await Send(session, "hello");

        session.LastTurn!.ContextFull.Should().BeTrue();
        session.LastTurn.Note.Should().BeNull();
    }

    [Fact]
    public void ShareOfTheWindow_NeedsBothNumbers()
    {
        Session(_ => FakeChatClient.StreamText("hi"), contextWindow: 2000).ContextShare.Should().BeNull();
    }

    private static async Task Send(AgentSession session, string text)
    {
        await foreach (var _ in session.SendAsync(text, default)) { }
    }

    // Like Unsloth Studio with include_usage: a last chunk with no text, only the usage.
    private static async IAsyncEnumerable<ChatResponseUpdate> WithUsage(
        IAsyncEnumerable<ChatResponseUpdate> reply, long prompt, long completion, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var update in reply.WithCancellation(ct))
            yield return update;
        yield return new ChatResponseUpdate(ChatRole.Assistant,
            [new UsageContent(new UsageDetails { InputTokenCount = prompt, OutputTokenCount = completion, TotalTokenCount = prompt + completion })]);
    }

    [Fact]
    public async Task IsTheServersCountOfPromptAndReply()
    {
        var session = Session(_ => WithUsage(FakeChatClient.StreamText("hi"), prompt: 1200, completion: 30));

        await Send(session, "hello");

        session.ContextTokens.Should().Be(1230);
    }

    [Fact]
    public async Task ToolTurn_KeepsTheLatestCallsCount()
    {
        // Call 1 asks for Glob; call 2 sees the tool result and answers, with a bigger prompt.
        var session = Session(messages => messages.Last().Role == ChatRole.User
            ? WithUsage(FakeChatClient.StreamToolCall("call-1", "Glob", []), prompt: 1000, completion: 10)
            : WithUsage(FakeChatClient.StreamText("one file"), prompt: 1500, completion: 20));

        await Send(session, "list files");

        session.ContextTokens.Should().Be(1520);
    }

    [Fact]
    public async Task ServerWithoutUsage_LeavesItUnknown()
    {
        var session = Session(_ => FakeChatClient.StreamText("hi"));

        await Send(session, "hello");

        session.ContextTokens.Should().BeNull();
    }

    [Fact]
    public async Task Clear_ForgetsIt()
    {
        var session = Session(_ => WithUsage(FakeChatClient.StreamText("hi"), prompt: 1200, completion: 30));
        await Send(session, "hello");

        session.Clear();

        session.ContextTokens.Should().BeNull();
    }

    [Fact]
    public async Task FailedTurn_PutsBackThePreviousCount()
    {
        // The failed turn is rolled back, so the count it reported no longer describes the history.
        var calls = 0;
        var session = Session(_ => ++calls == 1
            ? WithUsage(FakeChatClient.StreamText("hi"), prompt: 500, completion: 5)
            : FailAfter(WithUsage(FakeChatClient.StreamText("partial"), prompt: 900, completion: 5)));
        await Send(session, "hello");

        var second = async () => await Send(session, "again");

        await second.Should().ThrowAsync<InvalidOperationException>();
        session.ContextTokens.Should().Be(505);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> FailAfter(IAsyncEnumerable<ChatResponseUpdate> reply)
    {
        await foreach (var update in reply)
            yield return update;
        throw new InvalidOperationException("connection dropped");
    }
}
