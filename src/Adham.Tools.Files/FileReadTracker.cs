using System.Collections.Concurrent;

namespace Adham.Tools.Files;

// Which files the model has read this session, and what they looked like then.
// Edit and Write use it to refuse changing a file the model never looked at, or one
// that changed on disk since it was read.
public sealed class FileReadTracker
{
    private readonly ConcurrentDictionary<string, Fingerprint> _reads = new(StringComparer.OrdinalIgnoreCase);

    // A whole-file read. A later partial read never downgrades it.
    public void RecordFull(string fullPath) => Record(fullPath, full: true);

    // An offset/limit window: enough for Edit (old_string pins the change to what was seen),
    // not for Write (which replaces the whole file).
    public void RecordPartial(string fullPath) => Record(fullPath, full: false);

    public bool IsKnown(string fullPath) => _reads.ContainsKey(fullPath);

    public bool WasFullRead(string fullPath) => _reads.TryGetValue(fullPath, out var fp) && fp.Full;

    // True if the file's modification time or size differs from when it was read (or it's gone).
    public bool HasChangedSinceRead(string fullPath)
    {
        if (!_reads.TryGetValue(fullPath, out var seen) || !File.Exists(fullPath))
            return true;
        var info = new FileInfo(fullPath);
        return info.LastWriteTimeUtc != seen.WrittenUtc || info.Length != seen.Length;
    }

    // After our own change: update the fingerprint but keep the full/partial flag,
    // so a partial read followed by an Edit doesn't silently unlock Write.
    public void Refresh(string fullPath)
    {
        if (!File.Exists(fullPath) || !_reads.TryGetValue(fullPath, out var seen))
            return;
        var info = new FileInfo(fullPath);
        _reads[fullPath] = seen with { WrittenUtc = info.LastWriteTimeUtc, Length = info.Length };
    }

    public void Clear() => _reads.Clear();

    private void Record(string fullPath, bool full)
    {
        if (!File.Exists(fullPath))
            return;
        var info = new FileInfo(fullPath);
        _reads.AddOrUpdate(
            fullPath,
            new Fingerprint(info.LastWriteTimeUtc, info.Length, full),
            (_, existing) => new Fingerprint(info.LastWriteTimeUtc, info.Length, full || existing.Full));
    }

    private sealed record Fingerprint(DateTime WrittenUtc, long Length, bool Full);
}
