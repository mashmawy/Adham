using Adham.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Core.Tests;

// Why did the turn end? The CLI shows a note whenever the answer is missing or cut short,
// so "I had to type continue" can be traced to its real cause.
public class TurnOutcomeTests
{
    private static async Task<AgentSession> RunTurn(Func<IEnumerable<ChatMessage>, IAsyncEnumerable<ChatResponseUpdate>> model)
    {
        var tool = AIFunctionFactory.Create(() => "README.md", "Glob");
        var client = new ChatClientBuilder(new FakeChatClient(model)).UseFunctionInvocation().Build();
        var session = new AgentSession(client, new ChatOptions { Tools = [tool] }, systemPrompt: "sys");
        await foreach (var _ in session.SendAsync("go", default)) { }
        return session;
    }

    [Fact]
    public async Task NormalAnswer_HasNoNote()
    {
        var session = await RunTurn(_ => FakeChatClient.StreamText("done"));

        session.LastTurn!.FinishReason.Should().Be(ChatFinishReason.Stop);
        session.LastTurn.EndedWithText.Should().BeTrue();
        session.LastTurn.Note.Should().BeNull();
    }

    [Fact]
    public async Task CutOffAtTheLengthLimit_IsReported()
    {
        var session = await RunTurn(_ => FakeChatClient.StreamTextEndingWith("partial answ", ChatFinishReason.Length));

        session.LastTurn!.Note.Should().Contain("length limit");
    }

    [Fact]
    public async Task EmptyResponse_IsReported()
    {
        var session = await RunTurn(_ => FakeChatClient.StreamTextEndingWith("", finishReason: null));

        session.LastTurn!.EndedWithText.Should().BeFalse();
        session.LastTurn.Note.Should().Contain("empty response");
    }

    [Fact]
    public async Task NothingAfterTheToolResult_IsReported()
    {
        // The model calls a tool, gets the result, then ends the turn without saying anything.
        var session = await RunTurn(messages =>
            messages.Last().Role == ChatRole.Tool
                ? FakeChatClient.StreamTextEndingWith("", ChatFinishReason.Stop)
                : FakeChatClient.StreamToolCall("call-1", "Glob", []));

        session.LastTurn!.ToolCalls.Should().Be(1);
        session.LastTurn.EndedWithText.Should().BeFalse();
        session.LastTurn.Note.Should().Contain("after the tool call");
    }

    [Fact]
    public async Task TextBeforeTheToolCall_DoesNotCountAsTheAnswer()
    {
        // "Let me look at the files." → Glob → (nothing): still unanswered.
        var round = 0;
        var session = await RunTurn(messages =>
            ++round == 1
                ? Concat(FakeChatClient.StreamTextEndingWith("Let me look at the files.", null), FakeChatClient.StreamToolCall("c1", "Glob", []))
                : FakeChatClient.StreamTextEndingWith("", ChatFinishReason.Stop));

        session.LastTurn!.EndedWithText.Should().BeFalse();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Concat(
        IAsyncEnumerable<ChatResponseUpdate> first, IAsyncEnumerable<ChatResponseUpdate> second)
    {
        await foreach (var u in first) yield return u;
        await foreach (var u in second) yield return u;
    }
}
