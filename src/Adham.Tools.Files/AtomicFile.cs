namespace Adham.Tools.Files;

internal static class AtomicFile
{
    // Write to a temp file next to the target, then swap it in: a crash mid-write
    // never leaves a half-written file behind.
    public static async Task WriteAllTextAsync(string path, string contents, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(tmp, contents, ct).ConfigureAwait(false);
        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        else
            File.Move(tmp, path);
    }
}
