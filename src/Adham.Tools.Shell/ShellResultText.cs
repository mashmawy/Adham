using System.Globalization;
using System.Text;

namespace Adham.Tools.Shell;

// What the model sees after a command: the command, the exit code, then stdout and stderr.
internal static class ShellResultText
{
    // Small models otherwise answer a failed command with "something went wrong, what should I do?".
    internal const string FailureNote =
        "[The command failed. Fix the command and retry, or report the failure to the user. Don't ask the user for new instructions.]";

    public static string Format(Shell shell, string command, ShellResult result)
    {
        var sb = new StringBuilder()
            .Append(shell.Prompt).Append(' ').Append(command).Append('\n')
            .Append("exit_code=").Append(result.ExitCode.ToString(CultureInfo.InvariantCulture)).Append('\n');
        if (result.TimedOut)
            sb.Append("status=timeout (the process was stopped)\n");

        Section(sb, "stdout", result.Stdout, result.StdoutTruncated);
        Section(sb, "stderr", result.Stderr, result.StderrTruncated);

        if (result.ExitCode != 0 || result.TimedOut)
            sb.Append(FailureNote).Append('\n');
        return sb.ToString().TrimEnd('\n');
    }

    private static void Section(StringBuilder sb, string name, string text, bool truncated)
    {
        sb.Append(name).Append(":\n").Append(text);
        if (text.Length > 0 && !text.EndsWith('\n'))
            sb.Append('\n');
        if (truncated)
            sb.Append('[').Append(name).Append(" cut at ").Append(ShellRunner.MaxStreamBytes / 1024)
              .Append(" KB: narrow the command, or write the output to a file and Read it in parts]\n");
    }
}
