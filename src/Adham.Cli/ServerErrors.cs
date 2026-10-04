using System.ClientModel;

namespace Adham.Cli;

// The OpenAI SDK retries failed requests, then throws an AggregateException wrapping
// ClientResultException / HttpRequestException. All of those mean "the server didn't answer".
internal static class ServerErrors
{
    public static bool IsServerError(Exception ex) =>
        ex is HttpRequestException or ClientResultException
        || (ex is AggregateException agg && agg.InnerExceptions.All(IsServerError));

    // OpenAI's error code for "the request is bigger than the context window"; Unsloth Studio uses it too.
    public static bool IsContextOverflow(Exception ex) =>
        IsServerError(ex) && Describe(ex).Contains("context_length_exceeded", StringComparison.Ordinal);

    public static string Describe(Exception ex) =>
        (ex is AggregateException agg ? agg.InnerExceptions[0] : ex).Message;
}
