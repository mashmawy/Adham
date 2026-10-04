using Adham.Tools.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Tools.Abstractions.Tests;

public class ToolRegistryTests
{
    private sealed class StubTool(string name) : ITool
    {
        public string Name => name;
        public string Description => $"{name} does a thing.";
        public bool IsReadOnly => true;
        public AIFunction AsAIFunction() => AIFunctionFactory.Create(() => "ok", Name, Description);
    }

    [Fact]
    public void Find_ReturnsTheToolByExactName()
    {
        var registry = new ToolRegistry([new StubTool("Read"), new StubTool("Glob")]);

        registry.Find("Glob")!.Name.Should().Be("Glob");
        registry.Find("glob").Should().BeNull();
    }

    [Fact]
    public void AsAITools_ExposesEveryToolWithItsNameAndDescription()
    {
        var registry = new ToolRegistry([new StubTool("Read"), new StubTool("Glob")]);

        var tools = registry.AsAITools();

        tools.Select(t => t.Name).Should().BeEquivalentTo("Read", "Glob");
        tools.Should().OnlyContain(t => t.Description.EndsWith("does a thing.", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateNames_AreRejected()
    {
        var act = () => new ToolRegistry([new StubTool("Read"), new StubTool("Read")]);

        act.Should().Throw<ArgumentException>();
    }
}
