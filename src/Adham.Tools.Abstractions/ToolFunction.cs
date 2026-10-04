using Microsoft.Extensions.AI;

namespace Adham.Tools.Abstractions;

public static class ToolFunction
{
    // By default MEAI JSON-serializes return values, so a file read reaches the model as one
    // escaped string ("     1→a\n     2→b"). Tools return text meant to be read as-is.
    public static AIFunction Create(Delegate method, string name, string description) =>
        AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
        {
            Name = name,
            Description = description,
            MarshalResult = (result, _, _) => ValueTask.FromResult(result),
        });
}
