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

    public ValueTask<string> AskUserAsync(string question, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromResult("The user cancelled before answering.");

        output.Write($"\n  ? {question}\n  > ");
        var answer = input.ReadLine();

        if (answer is null)
            return ValueTask.FromResult("The user gave no answer (end of input).");

        var trimmed = answer.Trim();
        if (trimmed.Length == 0)
            return ValueTask.FromResult("The user gave no answer.");

        return ValueTask.FromResult(trimmed);
    }

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
