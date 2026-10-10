using Adham.Tools.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Tools.Abstractions.Tests;

public sealed class AskUserToolTests
{
    private sealed class FakeQuestioner : IUserQuestioner
    {
        public string? LastQuestion;
        public string[]? LastOptions;
        public string Answer = "test answer";

        public ValueTask<string> AskUserAsync(string question, string[]? options, CancellationToken cancellationToken)
        {
            LastQuestion = question;
            LastOptions = options;
            return ValueTask.FromResult(Answer);
        }
    }

    [Fact]
    public void Name_IsAskUser()
    {
        var tool = new AskUserTool(new FakeQuestioner());
        tool.Name.Should().Be("AskUser");
    }

    [Fact]
    public void IsReadOnly_IsTrue()
    {
        var tool = new AskUserTool(new FakeQuestioner());
        tool.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnsTheAnswerPrefixed()
    {
        var questioner = new FakeQuestioner();
        var tool = new AskUserTool(questioner);

        var result = await tool.AskAsync("What's the project name?", cancellationToken: CancellationToken.None);

        result.Should().Be("User response: test answer\nContinue the task with this response.");
        questioner.LastQuestion.Should().Be("What's the project name?");
    }

    [Fact]
    public async Task EmptyQuestion_ReturnsError()
    {
        var tool = new AskUserTool(new FakeQuestioner());

        var result = await tool.AskAsync("", cancellationToken: CancellationToken.None);

        result.Should().Be("Error: question is required.");
    }

    [Fact]
    public async Task NullQuestion_ReturnsError()
    {
        var tool = new AskUserTool(new FakeQuestioner());

        var result = await tool.AskAsync(null!, cancellationToken: CancellationToken.None);

        result.Should().Be("Error: question is required.");
    }

    [Fact]
    public async Task WhitespaceOnly_ReturnsError()
    {
        var tool = new AskUserTool(new FakeQuestioner());

        var result = await tool.AskAsync("   ", cancellationToken: CancellationToken.None);

        result.Should().Be("Error: question is required.");
    }

    [Fact]
    public void Schema_HasRequiredQuestion()
    {
        var tool = new AskUserTool(new FakeQuestioner());
        var schema = tool.AsAIFunction().JsonSchema.ToString();

        schema.Should().Contain("\"question\"").And.Contain("\"required\":[\"question\"]");
    }

    [Fact]
    public async Task WithOptions_PassesOptionsThrough()
    {
        var questioner = new FakeQuestioner();
        var tool = new AskUserTool(questioner);
        var options = new[] { "Yes", "No", "Maybe" };

        await tool.AskAsync("Pick one", options: options);

        questioner.LastOptions.Should().BeEquivalentTo(options);
    }

    [Fact]
    public async Task BlankOptions_AreDropped()
    {
        var questioner = new FakeQuestioner();

        await new AskUserTool(questioner).AskAsync("Pick one", options: ["MIT", "", "  ", " Apache-2.0 "]);

        questioner.LastOptions.Should().Equal("MIT", "Apache-2.0");
    }

    [Fact]
    public async Task OnlyBlankOptions_AskWithoutOptions()
    {
        var questioner = new FakeQuestioner();

        await new AskUserTool(questioner).AskAsync("Name?", options: ["", " "]);

        questioner.LastOptions.Should().BeNull();
    }

    [Fact]
    public void Schema_HasOptionalOptions()
    {
        var tool = new AskUserTool(new FakeQuestioner());
        var schema = tool.AsAIFunction().JsonSchema.ToString();

        schema.Should().Contain("\"options\"");
    }

    [Fact]
    public async Task InvokedThroughTheAIFunction_ReturnsPlainText()
    {
        var questioner = new FakeQuestioner();
        var function = new AskUserTool(questioner).AsAIFunction();

        var result = await function.InvokeAsync(new AIFunctionArguments
        {
            ["question"] = "Which license?",
            ["options"] = new[] { "MIT", "Apache-2.0" },
        });

        result.Should().Be("User response: test answer\nContinue the task with this response.");
        questioner.LastOptions.Should().Equal("MIT", "Apache-2.0");
    }
}
