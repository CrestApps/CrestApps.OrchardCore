using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;

namespace CrestApps.OrchardCore.Learning.Xapi.Core;

/// <summary>
/// What lists and reports show of a statement: the labels of its verb and object, the actor's name, and its result.
/// </summary>
public sealed class XapiStatementSummary
{
    /// <summary>Gets the label of the verb: its display in English or the first language, else the last part of its id.</summary>
    public string Verb { get; init; }

    /// <summary>Gets the label of the object: an activity's name, an agent's name or identifier, or a statement id.</summary>
    public string Object { get; init; }

    /// <summary>Gets the actor's name, when the statement gives one.</summary>
    public string ActorName { get; init; }

    /// <summary>Gets the result's success.</summary>
    public bool? Success { get; init; }

    /// <summary>Gets the result's completion.</summary>
    public bool? Completion { get; init; }

    /// <summary>Gets the result's scaled score.</summary>
    public decimal? ScoreScaled { get; init; }

    /// <summary>Gets the result's raw score.</summary>
    public decimal? ScoreRaw { get; init; }

    /// <summary>Gets the result's duration, as ISO 8601.</summary>
    public string Duration { get; init; }

    /// <summary>
    /// Summarizes a statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The summary.</returns>
    public static XapiStatementSummary Of(JsonObject statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var value = statement["object"] as JsonObject;
        var result = statement["result"] as JsonObject;

        return new XapiStatementSummary
        {
            Verb = Label(statement["verb"]?["display"] as JsonObject) ?? LastPart(XapiAgents.Text(statement["verb"]?["id"])),
            Object = XapiStatementValidator.ObjectTypeOf(value) switch
            {
                "Activity" => Label(value?["definition"]?["name"] as JsonObject) ?? XapiAgents.Text(value?["id"]),
                "Agent" or "Group" => XapiAgents.Text(value?["name"]) ?? XapiAgents.Key(value),
                "StatementRef" => XapiAgents.Text(value?["id"]),
                _ => XapiAgents.Text(value?["verb"]?["id"]) is { } subVerb ? LastPart(subVerb) : null,
            },
            ActorName = XapiAgents.Text(statement["actor"]?["name"]),
            Success = Boolean(result?["success"]),
            Completion = Boolean(result?["completion"]),
            ScoreScaled = Number(result?["score"]?["scaled"]),
            ScoreRaw = Number(result?["score"]?["raw"]),
            Duration = XapiAgents.Text(result?["duration"]),
        };
    }

    private static string Label(JsonObject map)
    {
        if (map is null || map.Count == 0)
        {
            return null;
        }

        foreach (var language in new[] { "en-US", "en" })
        {
            if (XapiAgents.Text(map[language]) is { } text)
            {
                return text;
            }
        }

        return XapiAgents.Text(map.First().Value);
    }

    private static string LastPart(string iri)
    {
        if (string.IsNullOrEmpty(iri))
        {
            return null;
        }

        var trimmed = iri.TrimEnd('/');
        var cut = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('#'));

        return cut >= 0 && cut < trimmed.Length - 1 ? trimmed[(cut + 1)..] : trimmed;
    }

    private static bool? Boolean(JsonNode node)
        => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    private static decimal? Number(JsonNode node)
        => XapiStatementValidator.TryGetNumber(node, out var number) ? number : null;
}
