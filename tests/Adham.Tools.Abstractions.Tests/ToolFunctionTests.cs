using System.Text.Json;
using Adham.Tools.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Adham.Tools.Abstractions.Tests;

public class ToolFunctionTests
{
    private const string Listing = "     1→first\n     2→second";

    [Fact]
    public async Task DefaultFactory_JsonEncodesStrings()
    {
        // The behavior ToolFunction exists to avoid.
        var fn = AIFunctionFactory.Create(() => Listing, "Read", "reads");

        var result = await fn.InvokeAsync();

        // A JsonElement whose wire form is a quoted string with \n escapes.
        result.Should().BeOfType<JsonElement>()
            .Which.GetRawText().Should().StartWith("\"").And.Contain("\\n");
    }

    [Fact]
    public async Task ToolFunction_ReturnsThePlainText()
    {
        var fn = ToolFunction.Create(() => Listing, "Read", "reads");

        var result = await fn.InvokeAsync();

        result.Should().Be(Listing);
    }

    [Fact]
    public async Task ToolFunction_AwaitsAsyncResults()
    {
        var fn = ToolFunction.Create(async () => { await Task.Yield(); return Listing; }, "Read", "reads");

        var result = await fn.InvokeAsync();

        result.Should().Be(Listing);
    }
}
