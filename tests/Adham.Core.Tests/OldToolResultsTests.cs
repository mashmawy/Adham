using Adham.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Core.Tests;

// Each model call gets a copy of the conversation where the newest tool rounds stay whole while they
// fit the budget, and long results in older rounds are notes. Seen live: one turn wrote and read
// back long documents until the context was full, so this has to work inside a turn too.
public class OldToolResultsTests
{
    // 2,000 characters, 40 lines: about one medium file.
    private static readonly string File = string.Join('\n', Enumerable.Range(1, 40).Select(i => $"{i,6}→line {i} of the file, padded to length....."));

    // One turn: the model reads `reads` files, one per round, then answers. Returns what each call was sent.
    private static async Task<(List<List<ChatMessage>> Sent, AgentSession Session)> ReadFiles(int reads, int keepChars, string? output = null)
    {
        var sent = new List<List<ChatMessage>>();
        var read = AIFunctionFactory.Create((string file_path) => output ?? File, "Read");
        var calls = 0;
        var model = new FakeChatClient(messages =>
        {
            sent.Add(messages.ToList());
            return calls < reads
                ? FakeChatClient.StreamToolCall($"call-{++calls}", "Read", new() { ["file_path"] = $"file{calls}.py" })
                : FakeChatClient.StreamText("read them");
        });
        var client = new ChatClientBuilder(model)
            .UseFunctionInvocation()
            .Use(inner => new ShortenedContextClient(inner, keepChars))
            .Build();
        var session = new AgentSession(client, new ChatOptions { Tools = [read] }, systemPrompt: "sys");

        await foreach (var _ in session.SendAsync("read the files", default)) { }
        return (sent, session);
    }

    private static List<string> Results(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(m => m.Contents.OfType<FunctionResultContent>()).Select(r => OldToolResults.Text(r.Result) ?? "").ToList();

    [Fact]
    public async Task LongTurn_ShortensTheOlderRounds_BeforeTheNextCall()
    {
        // Budget for two files: the last call sees reads 3 and 4 whole, 1 and 2 as notes.
        var (sent, _) = await ReadFiles(reads: 4, keepChars: 5_000);

        var results = Results(sent[^1]);
        results.Should().HaveCount(4);
        results[0].Should().Be("[Earlier result of Read(file_path: file1.py): 40 lines, removed to save context. Call Read again if you need it.]");
        results[1].Should().StartWith("[Earlier result of Read(file_path: file2.py)");
        results[2].Should().Be(File);
        results[3].Should().Be(File);
    }

    [Fact]
    public async Task WithinTheBudget_NothingIsShortened()
    {
        var (sent, _) = await ReadFiles(reads: 3, keepChars: 10_000);

        Results(sent[^1]).Should().AllBe(File);
    }

    [Fact]
    public async Task LatestRound_StaysWhole_EvenOverTheBudget()
    {
        // The model needs what it just asked for, however big.
        var (sent, _) = await ReadFiles(reads: 1, keepChars: 100);

        Results(sent[^1]).Should().Equal(File);
    }

    [Fact]
    public async Task TheHistory_KeepsEveryResultWhole()
    {
        var (_, session) = await ReadFiles(reads: 4, keepChars: 5_000);

        Results(session.History).Should().HaveCount(4).And.AllBe(File);
    }

    [Fact]
    public async Task ShortResults_AreKept()
    {
        var (sent, _) = await ReadFiles(reads: 4, keepChars: 10, output: "Edited calc.py");

        Results(sent[^1]).Should().AllBe("Edited calc.py");
    }

    // One turn: the model writes the same document `writes` times (as seen live), then answers.
    private static async Task<(List<List<ChatMessage>> Sent, AgentSession Session)> WriteDocument(int writes, int keepChars)
    {
        var sent = new List<List<ChatMessage>>();
        var write = AIFunctionFactory.Create((string file_path, string content) => "Wrote 40 lines", "Write");
        var calls = 0;
        var model = new FakeChatClient(messages =>
        {
            sent.Add(messages.ToList());
            return calls < writes
                ? FakeChatClient.StreamToolCall($"call-{++calls}", "Write", new() { ["file_path"] = "docs/design.md", ["content"] = File })
                : FakeChatClient.StreamText("written");
        });
        var client = new ChatClientBuilder(model)
            .UseFunctionInvocation()
            .Use(inner => new ShortenedContextClient(inner, keepChars))
            .Build();
        var session = new AgentSession(client, new ChatOptions { Tools = [write] }, systemPrompt: "sys");

        await foreach (var _ in session.SendAsync("write the design", default)) { }
        return (sent, session);
    }

    private static List<string?> Contents(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(m => m.Contents.OfType<FunctionCallContent>()).Select(c => OldToolResults.Text(c.Arguments!["content"])).ToList();

    [Fact]
    public async Task OldWrites_SendTheirContentAsANote()
    {
        // Budget for one document: the last call sees only the newest write's content.
        var (sent, _) = await WriteDocument(writes: 3, keepChars: 2_500);

        var contents = Contents(sent[^1]);
        contents.Should().HaveCount(3);
        contents[0].Should().Be("[40 lines, removed to save context]");
        contents[1].Should().Be("[40 lines, removed to save context]");
        contents[2].Should().Be(File);
    }

    [Fact]
    public async Task OldWrites_KeepTheirShortArguments()
    {
        var (sent, _) = await WriteDocument(writes: 3, keepChars: 2_500);

        sent[^1].SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Should().AllSatisfy(c => c.Arguments!["file_path"].Should().Be("docs/design.md"));
    }

    [Fact]
    public async Task OldWrites_StayWholeInTheHistory()
    {
        var (_, session) = await WriteDocument(writes: 3, keepChars: 2_500);

        Contents(session.History).Should().AllBe(File);
    }

    [Fact]
    public void Budget_IsAFifthOfTheWindow_InCharacters()
    {
        ShortenedContextClient.KeepCharsFor(32_768).Should().Be(26_214);
        ShortenedContextClient.KeepCharsFor(contextWindow: null).Should().Be(26_214);
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
