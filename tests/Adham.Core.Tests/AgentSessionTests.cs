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
}
