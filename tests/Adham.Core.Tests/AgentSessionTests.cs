using Adham.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Core.Tests;

public class AgentSessionTests
{
    [Fact]
    public async Task SendAsync_StreamsTheReplyChunkByChunk()
    {
        var client = new FakeChatClient(_ => FakeChatClient.StreamText("hello world"));
        var session = new AgentSession(client, new ChatOptions(), systemPrompt: "you are adham");

        var chunks = new List<string>();
        await foreach (var u in session.SendAsync("hi", default))
            chunks.Add(u.Text);

        chunks.Should().HaveCountGreaterThan(1);
        string.Concat(chunks).Should().Be("hello world");
    }

    [Fact]
    public async Task SendAsync_ResendsTheWholeConversationEveryTurn()
    {
        var sent = new List<int>();
        var client = new FakeChatClient(messages =>
        {
            sent.Add(messages.Count());
            return FakeChatClient.StreamText("ok");
        });
        var session = new AgentSession(client, new ChatOptions(), systemPrompt: "you are adham");

        await foreach (var _ in session.SendAsync("first", default)) { }
        await foreach (var _ in session.SendAsync("second", default)) { }

        sent.Should().Equal(2, 4); // system+user, then system+user+assistant+user
        session.History.Select(m => m.Role).Should().Equal(
            ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User, ChatRole.Assistant);
    }

    [Fact]
    public async Task FailedTurn_IsRolledBack_SoTheNextTurnStillWorks()
    {
        // The server rejects the first request (e.g. context_length_exceeded), then recovers.
        var sent = new List<int>();
        var client = new FakeChatClient(messages =>
        {
            sent.Add(messages.Count());
            return sent.Count == 1 ? Fail() : FakeChatClient.StreamText("ok");
        });
        var session = new AgentSession(client, new ChatOptions(), systemPrompt: "you are adham");

        var first = async () => { await foreach (var _ in session.SendAsync("too long", default)) { } };
        await first.Should().ThrowAsync<InvalidOperationException>();
        session.History.Select(m => m.Role).Should().Equal(ChatRole.System);

        await foreach (var _ in session.SendAsync("shorter", default)) { }

        sent.Should().Equal(2, 2); // the failed message wasn't re-sent
        session.History.Select(m => m.Text).Should().Equal("you are adham", "shorter", "ok");
    }

    [Fact]
    public async Task TurnThatFailsMidStream_IsRolledBack()
    {
        var client = new FakeChatClient(_ => HalfThenFail());
        var session = new AgentSession(client, new ChatOptions(), systemPrompt: "you are adham");

        var turn = async () => { await foreach (var _ in session.SendAsync("hi", default)) { } };

        await turn.Should().ThrowAsync<InvalidOperationException>();
        session.History.Select(m => m.Role).Should().Equal(ChatRole.System);
    }

    [Fact]
    public async Task Clear_KeepsTheSystemPrompt_AndForgetsTheConversation()
    {
        var client = new FakeChatClient(_ => FakeChatClient.StreamText("ok"));
        var session = new AgentSession(client, new ChatOptions(), systemPrompt: "you are adham");
        await foreach (var _ in session.SendAsync("hi", default)) { }

        session.Clear();

        session.History.Select(m => m.Text).Should().Equal("you are adham");
    }

    private static IAsyncEnumerable<ChatResponseUpdate> Fail() =>
        throw new InvalidOperationException("context_length_exceeded");

    private static async IAsyncEnumerable<ChatResponseUpdate> HalfThenFail()
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "partial ");
        await Task.Yield();
        throw new InvalidOperationException("connection dropped");
    }
}
