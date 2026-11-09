using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Webhooks;

/// <summary>
/// Declares the AI events webhook subscriptions can choose: conversations starting, ending, analyzed, transferred and
/// scored, and alerts triggering and resolving.
/// </summary>
public sealed class AIWebhookEventProvider : IWebhookEventProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookEventProvider"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIWebhookEventProvider(IStringLocalizer<AIWebhookEventProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public IEnumerable<WebhookEventDescriptor> GetEvents()
    {
        var group = S["AI"].Value;

        yield return new(AIWebhookEvents.ConversationStarted, S["AI conversation started"], group, new { channel = "Voice", direction = "Inbound", profileId = "4k5y9...", sessionId = "s1", activityId = "a1" });
        yield return new(AIWebhookEvents.ConversationEnded, S["AI conversation ended"], group, new { sessionId = "s1", profileId = "4k5y9...", reason = "Completed" });
        yield return new(AIWebhookEvents.ConversationAnalyzed, S["AI conversation analyzed"], group, new { sessionId = "s1", profileId = "4k5y9...", results = new { } });
        yield return new(AIWebhookEvents.ConversationTransferred, S["AI call handed to a person or transferred"], group, new { sessionId = "s1", target = "+15555550100" });
        yield return new(AIWebhookEvents.ConversationEvaluated, S["AI conversation scored"], group, new { sessionId = "s1", score = 0.92 });
        yield return new(AIWebhookEvents.AlertTriggered, S["AI alert triggered"], group, new { ruleId = "r1", value = 12 });
        yield return new(AIWebhookEvents.AlertResolved, S["AI alert resolved"], group, new { ruleId = "r1", value = 2 });
    }
}
