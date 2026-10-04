using System.ClientModel;

namespace Adham.Cli;

// The OpenAI SDK retries failed requests, then throws an AggregateException wrapping
// ClientResultException / HttpRequestException. All of those mean "the server didn't answer".
internal static class ServerErrors
{
    public static bool IsServerError(Exception ex) =>
        ex is HttpRequestException or ClientResultException
        || (ex is AggregateException agg && agg.InnerExceptions.All(IsServerError));

    public static string Describe(Exception ex) =>
        (ex is AggregateException agg ? agg.InnerExceptions[0] : ex).Message;
}
