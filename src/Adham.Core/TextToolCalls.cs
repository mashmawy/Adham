using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Adham.Core;

// Seen live: Qwen sometimes writes its tool call into the reply as text, in its own format,
// and the server passes it on as text. The turn then ends on "<tool_call><function=Edit>…" and
// the edit never runs. This reads such a call back into a real one.
internal static partial class TextToolCalls
{
    public const string Open = "<tool_call>";

    // Parses every <tool_call>…</tool_call> block in the text. Null if there is none, or one of them
    // can't be read: then the text is better shown as it is than half-run.
    public static List<FunctionCallContent>? Parse(string text, IList<AITool>? tools)
    {
        var blocks = Block().Matches(text);
        if (blocks.Count == 0)
            return null;

        var calls = new List<FunctionCallContent>();
        foreach (Match block in blocks)
        {
            if (ParseOne(block.Groups[1].Value.Trim(), tools) is not { } call)
                return null;
            calls.Add(call);
        }
        return calls;
    }

    private static FunctionCallContent? ParseOne(string body, IList<AITool>? tools)
    {
        // Qwen-Coder: <function=Edit><parameter=file_path>a.cs</parameter>…</function>
        if (Function().Match(body) is { Success: true } function)
        {
            var name = function.Groups[1].Value;
            var schema = Schema(tools, name);
            var args = new Dictionary<string, object?>();
            foreach (Match parameter in Parameter().Matches(function.Groups[2].Value))
                args[parameter.Groups[1].Value] = Typed(parameter.Groups[2].Value, schema, parameter.Groups[1].Value);
            return Call(name, args);
        }

        // Qwen JSON: {"name": "Read", "arguments": {"file_path": "a.cs"}}
        try
        {
            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("name", out var nameElement) || nameElement.GetString() is not { Length: > 0 } name)
                return null;
            var args = new Dictionary<string, object?>();
            if (json.RootElement.TryGetProperty("arguments", out var arguments) && arguments.ValueKind == JsonValueKind.Object)
                foreach (var argument in arguments.EnumerateObject())
                    args[argument.Name] = argument.Value.Clone();
            return Call(name, args);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static FunctionCallContent Call(string name, Dictionary<string, object?> args) =>
        new($"text-call-{Guid.NewGuid():N}", name, args);

    private static JsonElement? Schema(IList<AITool>? tools, string name) =>
        tools?.OfType<AIFunction>().FirstOrDefault(f => f.Name == name)?.JsonSchema;

    // The XML form has only text, so a number or a flag comes as "80" or "False".
    // The tool's schema says what type each parameter has.
    private static object? Typed(string raw, JsonElement? schema, string parameter)
    {
        var value = raw.StartsWith('\n') ? raw[1..] : raw;
        value = value.EndsWith('\n') ? value[..^1] : value;

        var type = schema is { } s && s.TryGetProperty("properties", out var properties)
            && properties.TryGetProperty(parameter, out var property) && property.TryGetProperty("type", out var typeElement)
            ? TypeName(typeElement)
            : "string";

        return type switch
        {
            "integer" when long.TryParse(value.Trim(), out var number) => number,
            "number" when double.TryParse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var number) => number,
            "boolean" when bool.TryParse(value.Trim(), out var flag) => flag,
            "array" or "object" when TryJson(value) is { } json => json,
            _ => value,
        };
    }

    // "type" can be a list, e.g. ["integer", "null"] for an optional int.
    private static string TypeName(JsonElement type) =>
        type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(t => t.GetString()).FirstOrDefault(t => t != "null") ?? "string"
            : type.GetString() ?? "string";

    private static JsonElement? TryJson(string value)
    {
        try
        {
            using var json = JsonDocument.Parse(value);
            return json.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"<tool_call>(.*?)</tool_call>", RegexOptions.Singleline)]
    private static partial Regex Block();

    [GeneratedRegex(@"^<function=([^>\s]+)>(.*)</function>$", RegexOptions.Singleline)]
    private static partial Regex Function();

    [GeneratedRegex(@"<parameter=([^>\s]+)>(.*?)</parameter>", RegexOptions.Singleline)]
    private static partial Regex Parameter();
}
