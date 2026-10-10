using System.Text.RegularExpressions;

namespace Adham.Tools.Files;

// To save context, the model is sent one-line notes such as "[310 lines, removed to save context]"
// in place of old tool output. Seen live: asked to fix a document it no longer saw, the model wrote
// that note as the file's whole content. Write and Edit refuse text with a line that is only a note.
// The notes come from Adham.Core (OldToolResults); they all end with "removed to save context...]".
internal static partial class ContextNotes
{
    public static string? Refusal(string? text, string parameter) =>
        text is not null && Note().Match(text) is { Success: true } match
            ? $"Error: {parameter} contains \"{match.Value.Trim()}\". That is a note standing in for text removed from your context, " +
              "not real content. Read the file to get its text, then try again."
            : null;

    [GeneratedRegex(@"^[ \t]*\[[^\[\]\n]*removed to save context[^\[\]\n]*\][ \t]*\r?$", RegexOptions.Multiline)]
    private static partial Regex Note();
}
