using Adham.Tools.Abstractions;

namespace Adham.Cli;

// Asks the user in the console before a file change or a command. Anything but "y"/"yes" is a no.
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

    // Ctrl+C (a cancelled token) throws, like every other cancelled step; the chat loop then exits.
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

    private ValueTask<bool> Ask(string question)
    {
        output.Write(question);
        var answer = input.ReadLine()?.Trim();
        var approved = string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
        output.WriteLine(approved ? "  (approved)" : "  (declined)");
        return ValueTask.FromResult(approved);
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
