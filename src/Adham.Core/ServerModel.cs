namespace Adham.Core;

// A model the server serves, and how many tokens fit in its context window (null when the server doesn't say).
public sealed record ServerModel(string Id, int? ContextLength);
