using Adham.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Core.Tests;

public class ToolLoopTests
{
    [Fact]
    public async Task ToolCall_IsExecuted_AndTheAnswerUsesItsResult()
    {
        var calls = new List<string>();
        var tool = AIFunctionFactory.Create((string name) => { calls.Add(name); return $"hello, {name}"; }, "Greet");

        // Scripted model: first asks for the tool, then answers once a tool result is in the conversation.
        var model = new FakeChatClient(messages =>
            messages.Last().Role == ChatRole.Tool
                ? FakeChatClient.StreamText("The tool said: " + messages.Last().Contents.OfType<FunctionResultContent>().Single().Result)
                : FakeChatClient.StreamToolCall("call-1", "Greet", new() { ["name"] = "Adham" }));

        var client = new ChatClientBuilder(model).UseFunctionInvocation().Build();
        var session = new AgentSession(client, new ChatOptions { Tools = [tool] }, systemPrompt: "sys");

        var text = "";
        await foreach (var u in session.SendAsync("greet Adham", default))
            text += u.Text;

        calls.Should().Equal("Adham");
        text.Should().Be("The tool said: hello, Adham");
    }

    [Fact]
    public async Task History_KeepsTheToolCallAndResult_ForTheNextTurn()
    {
        var tool = AIFunctionFactory.Create(() => "README.md", "Glob");
        var model = new FakeChatClient(messages =>
            messages.Last().Role == ChatRole.Tool
                ? FakeChatClient.StreamText("found it")
                : FakeChatClient.StreamToolCall("call-1", "Glob", []));

        var client = new ChatClientBuilder(model).UseFunctionInvocation().Build();
        var session = new AgentSession(client, new ChatOptions { Tools = [tool] }, systemPrompt: "sys");

        await foreach (var _ in session.SendAsync("find the readme", default)) { }

        session.History.Select(m => m.Role).Should().Equal(
            ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant);
        session.History[2].Contents.Should().ContainSingle(c => c is FunctionCallContent);
        session.History[3].Contents.Should().ContainSingle(c => c is FunctionResultContent);
    }
}
