using System.ComponentModel;
using System.Text;
using Adham.Tools.Abstractions;
using Microsoft.Extensions.AI;

namespace Adham.Tools.Files;

public sealed class WriteTool(WorkingDirectory cwd, FileReadTracker reads, IChangeApprover approver) : ITool
{
    public string Name => "Write";

    public string Description =>
        "Create a new file, or replace an existing file's entire content.\n" +
        "- file_path: path to the file, absolute or relative to the working directory. Missing folders are created.\n" +
        "- content: the complete new content of the file.\n" +
        "To overwrite an existing file you must Read it in full first (no offset/limit). " +
        "For a targeted change to an existing file, prefer Edit.";

    public bool IsReadOnly => false;

    public AIFunction AsAIFunction() =>
        ToolFunction.Create(ExecuteAsync, Name, Description);

#pragma warning disable CA1707
    internal async Task<string> ExecuteAsync(
        [Description("Path to the file, absolute or relative to the working directory.")] string file_path,
        [Description("The complete content to write.")] string content,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        if (string.IsNullOrWhiteSpace(file_path))
            return "Error: file_path is required.";

        var fullPath = cwd.Resolve(file_path);
        if (Directory.Exists(fullPath))
            return $"Error: {file_path} is a folder, not a file.";

        var exists = File.Exists(fullPath);
        if (exists && CheckOverwriteAllowed(fullPath) is { } refusal)
            return refusal;

        var before = exists ? await File.ReadAllTextAsync(fullPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false) : null;
        if (!await approver.ApproveAsync(new FileChange(file_path, before, content ?? ""), cancellationToken).ConfigureAwait(false))
            return Declined(file_path);

        await AtomicFile.WriteAllTextAsync(fullPath, content ?? "", cancellationToken).ConfigureAwait(false);

        // Our own write counts as a full read, so writing the same file again isn't blocked.
        reads.RecordFull(fullPath);

        return exists
            ? $"The file {file_path} has been updated successfully."
            : $"File created successfully at: {file_path}";
    }

    internal static string Declined(string path) =>
        $"The user declined this change to {path}. Nothing was written. " +
        "Don't retry the same change; ask the user what they want instead.";

    // Write replaces the whole file, so it needs whole-file consent: a full read, and no change since.
    private string? CheckOverwriteAllowed(string fullPath)
    {
        if (!reads.WasFullRead(fullPath))
            return new StringBuilder()
                .Append("Error: the file already exists and has not been fully read this session. ")
                .Append("Write replaces the entire file, so read it first to see what you would overwrite. ")
                .Append("Recovery: Read '").Append(fullPath).Append("' with no offset/limit, then call Write again ")
                .Append("(or use Edit for a targeted change).")
                .ToString();

        if (reads.HasChangedSinceRead(fullPath))
            return $"Error: the file has been modified since it was last read. Read it again before overwriting: {fullPath}";

        return null;
    }
}
