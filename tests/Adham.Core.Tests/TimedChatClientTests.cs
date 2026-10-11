using Adham.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Core.Tests;

public class TimedChatClientTests
{
    // As Unsloth Studio sent it, on the chunk that carries the usage.
    private const string LastChunk = """
        {"id":"chatcmpl-1","choices":[],"usage":{"completion_tokens":60,"prompt_tokens":16,"total_tokens":76},
         "timings":{"cache_n":4,"prompt_n":12,"prompt_ms":252.955,"prompt_per_second":63.2,"predicted_n":60,"predicted_ms":1866.142,"predicted_per_second":31.6}}
        """;

    [Fact]
    public void ServerTimings_AreReadFromTheChunk()
    {
        var timings = ServerTimings.Parse(LastChunk);

        timings.Should().Be(new ServerTimings(12, TimeSpan.FromMilliseconds(252.955), 4, 60, TimeSpan.FromMilliseconds(1866.142)));
    }

    [Theory]
    [InlineData("""{"id":"chatcmpl-1","choices":[]}""")] // a server without timings
    [InlineData("not json")]
    public void NoTimings_IsNull(string chunk)
    {
        ServerTimings.Parse(chunk).Should().BeNull();
    }

    [Fact]
    public async Task EachModelCall_IsReported_WithItsUsage()
    {
        var reported = new List<ModelCallTiming>();
        var calls = 0;
        var model = new FakeChatClient(_ => ++calls == 1
            ? FakeChatClient.StreamToolCall("c1", "Ping", [])
            : WithUsage(FakeChatClient.StreamText("pong"), input: 120, output: 7));
        var ping = AIFunctionFactory.Create(() => "ok", "Ping");
        var client = new ChatClientBuilder(model).UseFunctionInvocation().Use(inner => new TimedChatClient(inner, reported.Add)).Build();

        await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "ping")], new ChatOptions { Tools = [ping] })) { }

        reported.Should().HaveCount(2); // the tool call and the answer: one line each
        reported[0].FirstToken.Should().NotBeNull();
        reported[1].InputTokens.Should().Be(120);
        reported[1].OutputTokens.Should().Be(7);
        reported[1].Server.Should().BeNull(); // the fake server sends no raw chunks
        reported[1].Total.Should().BeGreaterThanOrEqualTo(reported[1].FirstToken!.Value);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> WithUsage(IAsyncEnumerable<ChatResponseUpdate> updates, long input, long output)
    {
        await foreach (var update in updates)
            yield return update;
        yield return new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = input, OutputTokenCount = output })]);
    }
}
