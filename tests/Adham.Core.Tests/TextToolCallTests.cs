using Adham.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Core.Tests;

// Seen live (Qwen3.6 in Unsloth Studio): the model wrote its tool call into the reply as text, the
// turn ended on it, and the edit never ran. The tool loop should run such a call like a real one.
public class TextToolCallTests
{
    // As it came in the live run: some text, then the call in Qwen-Coder's XML form.
    private const string LeakedEdit = """
        Let me fix both sections explicitly:

        <tool_call>
        <function=Edit>
        <parameter=file_path>
        src/Mediator.cs
        </parameter>
        <parameter=new_string>
                    var prev = pipeline;
                    pipeline = wrapped;
        </parameter>
        <parameter=old_string>
                    pipeline = prev;
        </parameter>
        <parameter=replace_all>
        False
        </parameter>
        </function>
        </tool_call>
        """;

    private sealed record Ran(string Tool, Dictionary<string, object?> Args);

    // The model sends `reply` first; once it sees a tool result it answers "done".
    private static async Task<(List<Ran> Ran, string Shown)> Run(string reply)
    {
        var ran = new List<Ran>();
        var edit = AIFunctionFactory.Create(
            (string file_path, string old_string, string new_string, bool? replace_all) =>
            {
                ran.Add(new("Edit", new() { ["file_path"] = file_path, ["old_string"] = old_string, ["new_string"] = new_string, ["replace_all"] = replace_all }));
                return "edited";
            }, "Edit");
        var read = AIFunctionFactory.Create(
            (string file_path, int? limit) =>
            {
                ran.Add(new("Read", new() { ["file_path"] = file_path, ["limit"] = limit }));
                return "1→text";
            }, "Read");

        var model = new FakeChatClient(messages => messages.Last().Role == ChatRole.Tool
            ? FakeChatClient.StreamText("done")
            : FakeChatClient.StreamText(reply)); // in 8-character chunks, so "<tool_call>" is split
        var client = new ChatClientBuilder(model).UseFunctionInvocation().Use(inner => new TextToolCallClient(inner)).Build();
        var session = new AgentSession(client, new ChatOptions { Tools = [edit, read] }, systemPrompt: "sys");

        var shown = new System.Text.StringBuilder();
        await foreach (var update in session.SendAsync("fix it", default))
            shown.Append(update.Text);
        return (ran, shown.ToString());
    }

    [Fact]
    public async Task XmlCall_InTheText_IsRun()
    {
        var (ran, shown) = await Run(LeakedEdit);

        ran.Should().ContainSingle();
        ran[0].Tool.Should().Be("Edit");
        ran[0].Args["file_path"].Should().Be("src/Mediator.cs");
        ran[0].Args["new_string"].Should().Be("            var prev = pipeline;\n            pipeline = wrapped;");
        ran[0].Args["old_string"].Should().Be("            pipeline = prev;");
        ran[0].Args["replace_all"].Should().Be(false);
        shown.Should().StartWith("Let me fix both sections explicitly:").And.EndWith("done").And.NotContain("<tool_call>");
    }

    [Fact]
    public async Task JsonCall_InTheText_IsRun()
    {
        var (ran, _) = await Run("""<tool_call>{"name": "Read", "arguments": {"file_path": "a.cs", "limit": 20}}</tool_call>""");

        ran.Should().ContainSingle().Which.Args.Should().Contain("file_path", "a.cs").And.Contain("limit", 20);
    }

    [Fact]
    public async Task XmlNumbers_FollowTheToolSchema()
    {
        var (ran, _) = await Run("<tool_call><function=Read><parameter=file_path>a.cs</parameter><parameter=limit>20</parameter></function></tool_call>");

        ran.Should().ContainSingle().Which.Args["limit"].Should().Be(20);
    }

    [Theory]
    [InlineData("<tool_call>not a call</tool_call>")]
    [InlineData("<tool_call><function=Read><parameter=file_path>a.cs</parameter>")] // cut off
    public async Task UnreadableCall_IsShownAsItCame(string reply)
    {
        var (ran, shown) = await Run(reply);

        ran.Should().BeEmpty();
        shown.Should().Be(reply);
    }

    [Theory]
    [InlineData("Use a < b here.")]
    [InlineData("It ends with <tool")] // a possible start of the tag that never completes
    public async Task OrdinaryText_IsShownUnchanged(string reply)
    {
        var (ran, shown) = await Run(reply);

        ran.Should().BeEmpty();
        shown.Should().Be(reply);
    }
}
