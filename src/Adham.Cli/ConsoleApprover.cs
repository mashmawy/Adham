using Adham.Tools.Abstractions;

namespace Adham.Cli;

// Asks the user in the console before a file change or a command: y approves, n or Enter declines.
internal sealed class ConsoleApprover(TextReader input, TextWriter output, bool color) : IChangeApprover, ICommandApprover, IUserQuestioner
{
    public static ConsoleApprover ForConsole() => new(Console.In, Console.Out, color: !Console.IsOutputRedirected);

    public ValueTask<bool> ApproveAsync(FileChange change, CancellationToken cancellationToken)
    {
        output.WriteLine();
        output.WriteLine(change.Before is null ? $"  New file: {change.Path}" : $"  Change to {change.Path}:");
        foreach (var (kind, text) in DiffText.Lines(change.Before, change.After))
            Write(kind switch { '+' => ConsoleColor.Green, '-' => ConsoleColor.Red, _ => ConsoleColor.DarkGray }, $"  {kind} {text}");

        return Ask($"  Allow this change to {change.Path}? [y/N] ");
    }

    public ValueTask<bool> ApproveAsync(CommandRequest request, CancellationToken cancellationToken)
    {
        output.WriteLine();
        output.WriteLine($"  Run in {request.Shell}:");
        Write(ConsoleColor.Yellow, $"    {request.Command}");

        return Ask("  Run this command? [y/N] ");
    }

    // Ctrl+C (a cancelled token) throws, before or while asking, like every other cancelled step;
    // the chat loop then exits.
    public ValueTask<string> AskUserAsync(string question, string[]? options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        output.Write($"\n  ? {question}");
        for (var i = 0; i < (options?.Length ?? 0); i++)
            output.Write($"\n    {i + 1}. {options![i]}");

        output.Write("\n  > ");
        while (true)
        {
            var answer = input.ReadLine();
            // ReadLine doesn't watch the token: Ctrl+C while waiting only shows up once it returns.
            cancellationToken.ThrowIfCancellationRequested();

            // Without an answer small models tend to make one up, so say what to do instead.
            if (answer is null)
                return ValueTask.FromResult($"The user gave no answer (end of input). {DontGuess}");

            var trimmed = answer.Trim();
            if (trimmed.Length == 0)
                return ValueTask.FromResult($"The user gave no answer. {DontGuess}");

            // A number picks that option; a number that isn't one is asked again, since the model
            // couldn't tell "5" the typo from "5" the answer. Anything else is the answer as typed.
            if (options is not { Length: > 0 } || !int.TryParse(trimmed, out var num))
                return ValueTask.FromResult(trimmed);
            if (num >= 1 && num <= options.Length)
            {
                output.WriteLine($"  ({options[num - 1]})");
                return ValueTask.FromResult(options[num - 1]);
            }

            output.Write($"  Pick 1-{options.Length}, or type an answer.\n  > ");
        }
    }

    private const string DontGuess = "Don't guess it: continue without it, or stop and say what you need.";

    // y/yes approves; n/no, Enter (the N in [y/N]) and end of input decline. Anything else is asked
    // again. Seen live: with the Arabic keyboard layout on, the Y key types "غ", and two approvals
    // were lost as declines.
    private ValueTask<bool> Ask(string question)
    {
        output.Write(question);
        while (true)
        {
            var answer = input.ReadLine()?.Trim();
            bool? approved = answer?.ToUpperInvariant() switch
            {
                "Y" or "YES" => true,
                null or "" or "N" or "NO" => false,
                _ => null,
            };
            if (approved is { } decided)
            {
                output.WriteLine(decided ? "  (approved)" : "  (declined)");
                return ValueTask.FromResult(decided);
            }
            output.Write($"  Type y or n (\"{answer}\" is neither; check the keyboard layout): ");
        }
    }

    private void Write(ConsoleColor colour, string line)
    {
        if (color)
            Console.ForegroundColor = colour;
        output.WriteLine(line);
        if (color)
            Console.ResetColor();
    }
}
