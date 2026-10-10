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
    // About 4K tokens per stream: the whole result stays in the context for the next turn too,
    // and a 64 KB build log (about 16K tokens) was half a 32K window on its own.
    public const int MaxStreamBytes = 16 * 1024;

    // Of that, the first part is kept from the start of the output and the rest from the end,
    // where test failures, build errors and summaries usually are.
    internal const int HeadBytes = 4 * 1024;

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

    // Keeps about maxBytes (UTF-8) of output: the start up to HeadBytes, then the latest lines.
    // Lines dropped in between are counted and marked. The process keeps running either way.
    private sealed class CappedText(int maxBytes)
    {
        private readonly StringBuilder _head = new();
        private readonly Queue<(string Line, int Bytes)> _tail = new();
        private int _headBytes;
        private int _tailBytes;
        private int _cutLines;
        private bool _headFull;

        public bool Truncated => _cutLines > 0;

        public string Text
        {
            get
            {
                var text = new StringBuilder(_head.ToString());
                if (_cutLines > 0)
                    text.Append("…[").Append(_cutLines).Append(" lines cut]…\n");
                foreach (var (line, _) in _tail)
                    text.Append(line).Append('\n');
                return text.ToString();
            }
        }

        public void AppendLine(string line)
        {
            // Colors are for terminals; to the model and the summary they're noise.
            line = AnsiEscape().Replace(line, "");
            var size = Encoding.UTF8.GetByteCount(line) + 1;
            if (!_headFull && _headBytes + size <= HeadBytes)
            {
                _head.Append(line).Append('\n');
                _headBytes += size;
                return;
            }

            _headFull = true;
            _tail.Enqueue((line, size));
            _tailBytes += size;
            while (_tailBytes > maxBytes - HeadBytes && _tail.Count > 0)
            {
                _tailBytes -= _tail.Dequeue().Bytes;
                _cutLines++;
            }
        }
    }

    // ESC [ ... final byte: color and cursor sequences, e.g. ESC[31;1m.
    [GeneratedRegex(@"\x1B\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscape();
}
