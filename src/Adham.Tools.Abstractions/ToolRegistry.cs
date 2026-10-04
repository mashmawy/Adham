using Microsoft.Extensions.AI;

namespace Adham.Tools.Abstractions;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _byName;

    public ToolRegistry(IEnumerable<ITool> tools)
        => _byName = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);

    public IReadOnlyList<ITool> All => _byName.Values.ToArray();

    public ITool? Find(string name) => _byName.GetValueOrDefault(name);

    // What goes into ChatOptions.Tools: the list the model can choose from.
    public IList<AITool> AsAITools() => All.Select(t => (AITool)t.AsAIFunction()).ToList();
}
