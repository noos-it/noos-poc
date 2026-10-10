using System.Text.Json;

namespace Noos.Orchestrator.Workflows;

/// <summary>
/// Reading a persona's structured answer inside a workflow expression. Workflow variables hold the raw JSON text rather than a
/// parsed tree on purpose: a string survives Elsa's state serialisation across a suspension unchanged, where a
/// <c>Dictionary&lt;string, object?&gt;</c> comes back with its values reshaped. Every workflow here suspends (human review), so
/// everything it must still be able to read afterwards is kept as text and parsed on demand.
/// </summary>
internal static class WorkflowJson
{
    public static JsonElement? Prop(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var root = JsonDocument.Parse(json).RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
                ? value.Clone()
                : null;
        }
        catch (JsonException)
        {
            // A persona that answered with prose rather than its schema is a missing value, not a crashed workflow:
            // the caller's fallback decides what that means (route defaults to a review, reviewable defaults to false).
            return null;
        }
    }

    public static string? Str(string? json, string name) =>
        Prop(json, name) is { ValueKind: JsonValueKind.String } s ? s.GetString() : null;

    public static bool Bool(string? json, string name) =>
        Prop(json, name) is { ValueKind: JsonValueKind.True };

    public static int? Int(string? json, string name) =>
        Prop(json, name) is { ValueKind: JsonValueKind.Number } n && n.TryGetInt32(out var i) ? i : null;

    public static double? Num(string? json, string name) =>
        Prop(json, name) is { ValueKind: JsonValueKind.Number } n ? n.GetDouble() : null;

    public static IReadOnlyList<string> Strings(string? json, string name) =>
        Prop(json, name) is { ValueKind: JsonValueKind.Array } a
            ? [.. a.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)]
            : [];

    /// <summary>
    /// The named properties of <paramref name="json"/>, when present, as a job input. An allowlist rather than a copy because the
    /// receiving persona's schema is <c>additionalProperties: false</c>: one extra field and the gateway rejects the job.
    /// </summary>
    public static Dictionary<string, object?> Pick(string? json, params string[] names)
    {
        var picked = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (Prop(json, name) is { } value)
            {
                picked[name] = value;
            }
        }

        return picked;
    }

    /// <summary>Adds a value under <paramref name="name"/> unless it is null or empty, which keeps optional fields off the job entirely.</summary>
    public static Dictionary<string, object?> With(this Dictionary<string, object?> input, string name, object? value)
    {
        if (value is not null && value is not string { Length: 0 })
        {
            input[name] = value;
        }

        return input;
    }
}
