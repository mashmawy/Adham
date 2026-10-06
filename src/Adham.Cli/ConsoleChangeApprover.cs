using Adham.Tools.Abstractions;

namespace Adham.Cli;

// Shows the diff of a proposed file change and asks the user. Anything but "y"/"yes" is a no.
internal sealed class ConsoleChangeApprover(TextReader input, TextWriter output, bool color) : IChangeApprover
{
    public static ConsoleChangeApprover ForConsole() => new(Console.In, Console.Out, color: !Console.IsOutputRedirected);

    public ValueTask<bool> ApproveAsync(FileChange change, CancellationToken cancellationToken)
    {
        output.WriteLine();
        output.WriteLine(change.Before is null ? $"  New file: {change.Path}" : $"  Change to {change.Path}:");
        foreach (var (kind, text) in DiffText.Lines(change.Before, change.After))
            Write(kind, $"  {kind} {text}");

        output.Write($"  Allow this change to {change.Path}? [y/N] ");
        var answer = input.ReadLine()?.Trim();
        var approved = string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
        output.WriteLine(approved ? "  (approved)" : "  (declined)");
        return ValueTask.FromResult(approved);
    }

    private void Write(char kind, string line)
    {
        if (color)
            Console.ForegroundColor = kind switch
            {
                '+' => ConsoleColor.Green,
                '-' => ConsoleColor.Red,
                _ => ConsoleColor.DarkGray,
            };
        output.WriteLine(line);
        if (color)
            Console.ResetColor();
    }
}
