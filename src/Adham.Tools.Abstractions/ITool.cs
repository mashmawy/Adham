using Microsoft.Extensions.AI;

namespace Adham.Tools.Abstractions;

// Something the model can call. The model only ever sees Name, Description and the
// JSON schema of AsAIFunction(), so those are the tool's real interface.
public interface ITool
{
    string Name { get; }
    string Description { get; }

    // Read-only tools never change anything; later, the permission gate auto-allows them.
    bool IsReadOnly { get; }

    AIFunction AsAIFunction();
}
