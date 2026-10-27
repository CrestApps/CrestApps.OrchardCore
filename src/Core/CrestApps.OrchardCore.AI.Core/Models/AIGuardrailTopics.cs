namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Guardrail topics are prompt templates in the <see cref="Category"/> category: the template's title names the
/// topic and its body is the definition the utility model checks against. The AI module ships the ones below in its
/// <c>Templates/Prompts</c> folder; more are added under <c>Artificial Intelligence &gt; Templates</c> as System Prompt
/// templates in that category, and travel with recipes and deployment plans like any other template.
/// </summary>
public static class AIGuardrailTopics
{
    /// <summary>
    /// The template category guardrail topics are listed from.
    /// </summary>
    public const string Category = "Guardrail topic";

    /// <summary>
    /// Insults, threats or abuse aimed at a person.
    /// </summary>
    public const string Harassment = "guardrail-topic-harassment";

    /// <summary>
    /// Attacks on people for who they are.
    /// </summary>
    public const string Hate = "guardrail-topic-hate";

    /// <summary>
    /// Suicide, self-harm or eating disorders.
    /// </summary>
    public const string SelfHarm = "guardrail-topic-self-harm";

    /// <summary>
    /// Sexual content.
    /// </summary>
    public const string Sexual = "guardrail-topic-sexual";

    /// <summary>
    /// Violence or weapons.
    /// </summary>
    public const string Violence = "guardrail-topic-violence";

    /// <summary>
    /// Help with something illegal.
    /// </summary>
    public const string IllegalActivity = "guardrail-topic-illegal-activity";

    /// <summary>
    /// Personal medical, legal or financial advice.
    /// </summary>
    public const string RegulatedAdvice = "guardrail-topic-regulated-advice";

    /// <summary>
    /// Attempts to make the AI ignore its instructions or reveal them.
    /// </summary>
    public const string Jailbreak = "guardrail-topic-jailbreak";

    /// <summary>
    /// The topic of a verdict that came from the profile's own description of what else it refuses.
    /// </summary>
    public const string Other = "other";

    /// <summary>
    /// The topics the AI module ships.
    /// </summary>
    public static readonly IReadOnlyList<string> BuiltIn =
    [
        Harassment,
        Hate,
        SelfHarm,
        Sexual,
        Violence,
        IllegalActivity,
        RegulatedAdvice,
        Jailbreak,
    ];

    /// <summary>
    /// Keeps the template identifiers of a selection once each, trimmed and in the order given.
    /// </summary>
    /// <param name="topics">The selection.</param>
    public static string[] Normalize(IEnumerable<string> topics)
        => (topics ?? [])
            .Where(topic => !string.IsNullOrWhiteSpace(topic) && !string.Equals(topic.Trim(), Other, StringComparison.OrdinalIgnoreCase))
            .Select(topic => topic.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

/// <summary>
/// A guardrail topic a profile refuses, read from its template.
/// </summary>
/// <param name="Id">The template identifier, which the utility model names in its answer.</param>
/// <param name="Title">The topic's title.</param>
/// <param name="Definition">What falls under the topic.</param>
public sealed record AIGuardrailTopic(string Id, string Title, string Definition);
