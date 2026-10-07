using System.Text;

namespace Adham.Tools.Shell;

// One simple command from a compound line, e.g. "git status" from "git status && ls".
public sealed record CommandSegment(string Text, IReadOnlyList<string> Words)
{
    public string Name => Words.Count > 0 ? Words[0] : "";
}

// A command line split at && || ; | (and a lone & in bash), outside quotes.
// Risky is set when the line contains something whose effect can't be read from the text:
// command substitution, variables, redirects, newlines, escapes, script blocks.
// Only lines that are not risky can ever be auto-allowed.
public sealed record SplitCommand(IReadOnlyList<CommandSegment> Segments, string? Risky)
{
    public bool IsRisky => Risky is not null;
}

public static class CommandSplitter
{
    public static SplitCommand Split(string command, ShellKind kind)
    {
        var segments = new List<CommandSegment>();
        var words = new List<string>();
        var word = new StringBuilder();
        var segmentText = new StringBuilder();
        var hasWord = false;
        string? risky = null;
        var escape = kind == ShellKind.PowerShell ? '`' : '\\';

        void Risk(string why) => risky ??= why;

        void EndWord()
        {
            if (hasWord)
                words.Add(word.ToString());
            word.Clear();
            hasWord = false;
        }

        void EndSegment()
        {
            EndWord();
            var text = segmentText.ToString().Trim();
            if (text.Length > 0)
                segments.Add(new CommandSegment(text, words.ToArray()));
            words.Clear();
            segmentText.Clear();
        }

        var i = 0;
        while (i < command.Length)
        {
            var c = command[i];

            if (c is '\n' or '\r')
            {
                Risk("a newline (several commands in one call)");
                EndSegment();
                i++;
                continue;
            }

            if (c == '\'')
            {
                // Single quotes are literal in both shells.
                var close = command.IndexOf('\'', i + 1);
                if (close < 0) { Risk("an unclosed quote"); close = command.Length - 1; }
                word.Append(command, i + 1, Math.Max(0, close - i - 1));
                segmentText.Append(command, i, close - i + 1);
                hasWord = true;
                i = close + 1;
                continue;
            }

            if (c == '"')
            {
                // Double quotes still expand variables and substitutions.
                var j = i + 1;
                while (j < command.Length && command[j] != '"')
                {
                    if (command[j] == escape && j + 1 < command.Length) { Risk("an escape character"); word.Append(command[j + 1]); j += 2; continue; }
                    if (command[j] == '$' || (kind == ShellKind.Bash && command[j] == '`')) Risk("a variable or substitution inside quotes");
                    word.Append(command[j]);
                    j++;
                }
                if (j >= command.Length) Risk("an unclosed quote");
                segmentText.Append(command, i, Math.Min(j + 1, command.Length) - i);
                hasWord = true;
                i = j + 1;
                continue;
            }

            if (c == escape)
            {
                Risk("an escape character");
                if (i + 1 < command.Length) { word.Append(command[i + 1]); segmentText.Append(command, i, 2); }
                hasWord = true;
                i += 2;
                continue;
            }

            // Separators: && || ; | and (bash) a lone & that runs the left side in the background.
            if (c is '&' or '|' or ';')
            {
                var doubled = i + 1 < command.Length && command[i + 1] == c && c != ';';
                if (c == '&' && !doubled)
                {
                    if (kind == ShellKind.PowerShell) Risk("the & call operator");
                    else Risk("a background job (&)");
                }
                EndSegment();
                i += doubled ? 2 : 1;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                EndWord();
                segmentText.Append(c);
                i++;
                continue;
            }

            if (c is '>' or '<')
            {
                var rest = command[i..];
                if (rest.StartsWith(">&1", StringComparison.Ordinal) && word.ToString() == "2")
                {
                    // 2>&1: merge stderr into stdout, harmless.
                    word.Append(">&1");
                    segmentText.Append(">&1");
                    i += 3;
                    continue;
                }
                Risk("a redirect (< or >)");
            }

            if (c == '$') Risk(kind == ShellKind.PowerShell ? "a variable or subexpression ($)" : "a variable or substitution ($)");
            if (c == '`' && kind == ShellKind.Bash) Risk("command substitution (backticks)");
            if (kind == ShellKind.PowerShell)
            {
                if (c is '(' or ')') Risk("a parenthesized expression");
                if (c is '{' or '}') Risk("a script block");
                if (c == '@') Risk("splatting or an array expression (@)");
                if (c == ':' && i + 1 < command.Length && command[i + 1] == ':') Risk("a .NET static call (::)");
            }
            else if (c is '(' or ')')
            {
                Risk("a subshell or substitution");
            }

            word.Append(c);
            segmentText.Append(c);
            hasWord = true;
            i++;
        }

        EndSegment();
        if (kind == ShellKind.PowerShell && segments.Exists(s => s.Words.Contains("--%")))
            Risk("the stop-parsing token (--%)");
        return new SplitCommand(segments, risky);
    }
}
