using Adham.Tools.Abstractions;
using FluentAssertions;
using Xunit;

namespace Adham.Tools.Abstractions.Tests;

public sealed class AskUserToolTests
{
    private sealed class FakeQuestioner : IUserQuestioner
    {
        public string? LastQuestion;
        public string Answer = "test answer";

        public ValueTask<string> AskUserAsync(string question, CancellationToken cancellationToken)
        {
            LastQuestion = question;
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
        var approver = new FakeQuestioner();
        var tool = new AskUserTool(approver);

        var result = await tool.AskAsync("What's the project name?", CancellationToken.None);

        result.Should().Be("User response: test answer");
        approver.LastQuestion.Should().Be("What's the project name?");
    }

    [Fact]
    public async Task EmptyQuestion_ReturnsError()
    {
        var tool = new AskUserTool(new FakeQuestioner());

        var result = await tool.AskAsync("", CancellationToken.None);

        result.Should().Be("Error: question is required.");
    }

    [Fact]
    public async Task NullQuestion_ReturnsError()
    {
        var tool = new AskUserTool(new FakeQuestioner());

        var result = await tool.AskAsync(null!, CancellationToken.None);

        result.Should().Be("Error: question is required.");
    }

    [Fact]
    public async Task WhitespaceOnly_ReturnsError()
    {
        var tool = new AskUserTool(new FakeQuestioner());

        var result = await tool.AskAsync("   ", CancellationToken.None);

        result.Should().Be("Error: question is required.");
    }

    [Fact]
    public void Schema_HasRequiredQuestion()
    {
        var tool = new AskUserTool(new FakeQuestioner());
        var schema = tool.AsAIFunction().JsonSchema.ToString();

        schema.Should().Contain("\"question\"").And.Contain("\"required\":[\"question\"]");
    }
}
