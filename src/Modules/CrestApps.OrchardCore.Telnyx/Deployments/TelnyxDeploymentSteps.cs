using CrestApps.OrchardCore.Telnyx.Models;

namespace CrestApps.OrchardCore.Telnyx.Deployments;

/// <summary>
/// Names the recipe steps that carry Telnyx provider settings between environments, and the settings members that hold
/// data-protected secrets.
/// </summary>
public static class TelnyxDeploymentSteps
{
    /// <summary>
    /// The recipe step that carries the Telnyx voice provider settings.
    /// </summary>
    public const string Settings = "TelnyxSettings";

    /// <summary>
    /// The recipe step that carries the Telnyx SMS provider settings.
    /// </summary>
    public const string SmsSettings = "TelnyxSmsSettings";

    /// <summary>
    /// The voice settings members that are stored data-protected, keyed by member name, with the protection purpose
    /// the settings screen uses for each. These members are never exported.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> VoiceProtectedMembers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(TelnyxSettings.ApiKey)] = TelnyxConstants.ProtectorName,
        [nameof(TelnyxSettings.WebhookPublicKey)] = TelnyxConstants.WebhookProtectorName,
        [nameof(TelnyxSettings.TurnCredential)] = TelnyxConstants.ProtectorName,
    };

    /// <summary>
    /// The SMS settings members that are stored data-protected, keyed by member name, with the protection purpose
    /// the settings screen uses for each. These members are never exported.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> SmsProtectedMembers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(TelnyxSmsSettings.ApiKey)] = TelnyxConstants.SmsApiKeyProtectorName,
        [nameof(TelnyxSmsSettings.WebhookPublicKey)] = TelnyxConstants.SmsWebhookProtectorName,
    };
}
