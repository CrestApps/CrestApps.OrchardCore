using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telnyx.AI;

/// <summary>
/// Tells whether the Telnyx provider is enabled and connected, as the Telnyx settings' connection status does. The API
/// key, the webhook public key and the TURN credential are reported as present or not, never returned.
/// </summary>
internal sealed class GetTelnyxConnectionStatusTool : TelnyxToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getTelnyxConnectionStatus";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Tells whether the Telnyx provider is enabled and connected (connection ids present) and returns the default caller id. Never returns the API key.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var settings = (await services.GetRequiredService<ISiteService>().GetSiteSettingsAsync()).GetOrCreate<TelnyxSettings>();
        var hasVoiceApplication = !string.IsNullOrWhiteSpace(settings.ConnectionId);
        var hasSipConnection = !string.IsNullOrWhiteSpace(settings.SipConnectionId);
        var hasApiKey = !string.IsNullOrWhiteSpace(settings.ApiKey);

        return AIModuleTools.Json(new
        {
            enabled = settings.IsEnabled,
            connected = hasVoiceApplication && hasSipConnection,
            hasApiKey,
            hasVoiceApplication,
            hasSipConnection,
            hasOutboundVoiceProfile = !string.IsNullOrWhiteSpace(settings.OutboundVoiceProfileId),
            hasWebhookPublicKey = !string.IsNullOrWhiteSpace(settings.WebhookPublicKey),
            defaultCallerId = settings.DefaultOutboundCallerId,
            note = !settings.IsEnabled || !hasApiKey
                ? "Telnyx is not ready: enable it and save its API key in Settings, Communication, Telephony. Keys are entered there by a person, never through the assistant."
                : !(hasVoiceApplication && hasSipConnection)
                    ? "Telnyx is enabled but not connected: a person connects it with the Connect button in the Telnyx settings."
                    : null,
        });
    }
}
