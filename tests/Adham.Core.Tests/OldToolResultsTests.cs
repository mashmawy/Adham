using Adham.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Core.Tests;

// Long tool results from before the previous turn become one-line notes, so file reads
// don't fill the context for good. The previous turn's results stay for follow-ups.
public class OldToolResultsTests
{
    private static readonly string LongFile = string.Join('\n', Enumerable.Range(1, 40).Select(i => $"{i,6}→line {i} of the file"));

    // Each user turn makes the model Read once, then answer. Returns what the model was sent on each call.
    private static async Task<List<List<ChatMessage>>> RunTurns(int turns, string toolOutput)
    {
        var sent = new List<List<ChatMessage>>();
        var read = AIFunctionFactory.Create((string file_path) => toolOutput, "Read");
        var calls = 0;
        var model = new FakeChatClient(messages =>
        {
            var list = messages.ToList();
            sent.Add(list);
            return list[^1].Role == ChatRole.User
                ? FakeChatClient.StreamToolCall($"call-{++calls}", "Read", new() { ["file_path"] = $"file{calls}.py" })
                : FakeChatClient.StreamText("read it");
        });
        var client = new ChatClientBuilder(model).UseFunctionInvocation().Build();
        var session = new AgentSession(client, new ChatOptions { Tools = [read] }, systemPrompt: "sys");

        for (var i = 1; i <= turns; i++)
            await foreach (var _ in session.SendAsync($"turn {i}", default)) { }
        return sent;
    }

    private static List<string> Results(List<ChatMessage> messages) =>
        messages.SelectMany(m => m.Contents.OfType<FunctionResultContent>()).Select(r => r.Result?.ToString() ?? "").ToList();

    [Fact]
    public async Task ThirdTurn_SeesTheFirstReadAsANote_AndTheSecondInFull()
    {
        var sent = await RunTurns(3, LongFile);

        var results = Results(sent[^1]);
        results.Should().HaveCount(3);
        results[0].Should().Be("[Earlier result of Read(file_path: file1.py): 40 lines, removed to save context. Call Read again if you need it.]");
        results[1].Should().Contain("line 40 of the file");
        results[2].Should().Contain("line 40 of the file");
    }

    [Fact]
    public async Task SecondTurn_StillSeesTheFirstTurnInFull()
    {
        // e.g. "continue" right after a turn: the evidence is still there.
        var sent = await RunTurns(2, LongFile);

        Results(sent[^1]).Should().AllSatisfy(r => r.Should().Contain("line 40 of the file"));
    }

    [Fact]
    public async Task ShortResults_AreKept()
    {
        var sent = await RunTurns(3, "Edited calc.py");

        Results(sent[^1]).Should().AllSatisfy(r => r.Should().Contain("Edited calc.py"));
    }

    [Fact]
    public void ResultWithoutItsCall_GetsAPlainNote()
    {
        OldToolResults.Note(call: null, "a\nb\nc").Should().Be("[Earlier tool result (3 lines) removed to save context.]");
    }

    [Fact]
    public void LongArguments_AreClipped()
    {
        var call = new FunctionCallContent("c1", "PowerShell", new Dictionary<string, object?> { ["command"] = new string('x', 200) });

        OldToolResults.Note(call, "out").Should().Contain(new string('x', 80) + "…)").And.NotContain(new string('x', 81));
    }
}
