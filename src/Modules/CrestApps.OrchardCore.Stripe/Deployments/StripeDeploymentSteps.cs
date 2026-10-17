using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Models;

namespace CrestApps.OrchardCore.Stripe.Deployments;

/// <summary>
/// Names the recipe step that carries the Stripe settings between environments, the settings members that hold
/// data-protected secrets, and the members that belong to the environment that stored them.
/// </summary>
public static class StripeDeploymentSteps
{
    /// <summary>
    /// The recipe step that carries the Stripe settings.
    /// </summary>
    public const string Settings = "StripeSettings";

    /// <summary>
    /// The settings members that are stored data-protected, keyed by member name, with the protection purpose the
    /// settings page uses for each. These members are never exported; an import protects a clear-text value it carries.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ProtectedMembers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(StripeSettings.LivePrivateSecret)] = StripeSettingsDisplayDriver.ProtectionPurpose,
        [nameof(StripeSettings.LiveWebhookSecret)] = StripeSettingsDisplayDriver.ProtectionPurpose,
        [nameof(StripeSettings.TestPrivateSecret)] = StripeSettingsDisplayDriver.ProtectionPurpose,
        [nameof(StripeSettings.TestWebhookSecret)] = StripeSettingsDisplayDriver.ProtectionPurpose,
    };

    /// <summary>
    /// The settings members that describe this environment's connection to Stripe rather than its configuration: the
    /// verified account and the webhook endpoint created for this site's address. They are never exported and an
    /// import ignores them, because a destination that adopted them would believe it is connected and, on its next
    /// connect or disconnect, would delete the source site's webhook at Stripe.
    /// </summary>
    public static readonly IReadOnlySet<string> EnvironmentOwnedMembers = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(StripeSettings.LiveAccountId),
        nameof(StripeSettings.LiveWebhookId),
        nameof(StripeSettings.TestAccountId),
        nameof(StripeSettings.TestWebhookId),
    };
}
