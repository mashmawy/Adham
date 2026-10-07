using System.Text;
using System.Text.RegularExpressions;
using CliWrap;

namespace Adham.Tools.Shell;

public sealed record ShellResult(int ExitCode, string Stdout, string Stderr, bool TimedOut, bool StdoutTruncated, bool StderrTruncated);

// Runs one command in a fresh shell process in the working directory. Nothing carries over
// between calls (no cd, no variables). On timeout the process is killed and whatever it
// printed so far is returned.
public static partial class ShellRunner
{
    public const int DefaultTimeoutMs = 120_000;
    public const int MaxTimeoutMs = 600_000;
    public const int MaxStreamBytes = 64 * 1024;

    public static int ClampTimeout(int? timeoutMs) =>
        timeoutMs is > 0 ? Math.Min(timeoutMs.Value, MaxTimeoutMs) : DefaultTimeoutMs;

    public static async Task<ShellResult> RunAsync(Shell shell, string command, string workingDirectory, int timeoutMs, CancellationToken ct)
    {
        var stdout = new CappedText(MaxStreamBytes);
        var stderr = new CappedText(MaxStreamBytes);

        using var timeout = new CancellationTokenSource(timeoutMs);
        using var killWhen = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        var cmd = Cli.Wrap(shell.Executable)
            .WithArguments(shell.Arguments(command))
            .WithWorkingDirectory(workingDirectory)
            .WithValidation(CommandResultValidation.None)
            .WithStandardInputPipe(PipeSource.Null)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(stdout.AppendLine, Encoding.UTF8))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(stderr.AppendLine, Encoding.UTF8));

        try
        {
            var result = await cmd.ExecuteAsync(killWhen.Token).ConfigureAwait(false);
            return new ShellResult(result.ExitCode, stdout.Text, stderr.Text, false, stdout.Truncated, stderr.Truncated);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return new ShellResult(-1, stdout.Text, stderr.Text, true, stdout.Truncated, stderr.Truncated);
        }
    }

    // Keeps the first maxBytes (UTF-8) of output; the process keeps running, the rest is dropped.
    private sealed class CappedText(int maxBytes)
    {
        private readonly StringBuilder _text = new();
        private int _bytes;

        public bool Truncated { get; private set; }

        public string Text => _text.ToString();

        public void AppendLine(string line)
        {
            if (Truncated)
                return;
            // Colors are for terminals; to the model and the summary they're noise.
            line = AnsiEscape().Replace(line, "");
            var size = Encoding.UTF8.GetByteCount(line) + 1;
            if (_bytes + size > maxBytes)
            {
                Truncated = true;
                return;
            }
            _text.Append(line).Append('\n');
            _bytes += size;
        }
    }

    // ESC [ ... final byte: color and cursor sequences, e.g. ESC[31;1m.
    [GeneratedRegex(@"\x1B\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscape();
}
