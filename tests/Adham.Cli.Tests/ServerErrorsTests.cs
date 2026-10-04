using System.ClientModel;
using Adham.Cli;
using FluentAssertions;
using Xunit;

namespace Adham.Cli.Tests;

public class ServerErrorsTests
{
    private const string Refused = "No connection could be made because the target machine actively refused it.";

    [Fact]
    public void RetriedConnectionFailure_IsAServerError_WithTheFirstInnerMessage()
    {
        // What the OpenAI SDK throws after its retries give up.
        var ex = new AggregateException(
            "Retry failed after 4 tries.",
            new ClientResultException(Refused, innerException: new HttpRequestException(Refused)),
            new ClientResultException(Refused, innerException: new HttpRequestException(Refused)));

        ServerErrors.IsServerError(ex).Should().BeTrue();
        ServerErrors.Describe(ex).Should().Be(Refused);
    }

    [Fact]
    public void PlainHttpFailure_IsAServerError()
    {
        ServerErrors.IsServerError(new HttpRequestException(Refused)).Should().BeTrue();
    }

    [Fact]
    public void UnrelatedAggregate_IsNotSwallowed()
    {
        var ex = new AggregateException(new InvalidOperationException("bug"));

        ServerErrors.IsServerError(ex).Should().BeFalse();
    }
}
