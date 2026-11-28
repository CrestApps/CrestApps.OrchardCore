using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Stripe.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Stripe.AI;

/// <summary>
/// Reports the Stripe connection as Settings › Payments › Stripe shows it: live or test mode, whether an account is
/// connected for each environment (<see cref="IStripeConnectService.IsConnectedAsync"/>) and its account id, and which
/// keys are set and whether each comes from the site or from the application configuration. No key or secret is
/// returned, only whether it is set.
/// </summary>
internal sealed class GetStripeConnectionStatusTool : StripeAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getStripeConnectionStatus";

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
    public override string Description => "Reports whether Stripe is in live or test mode, whether an account is connected for each, the connected account id, and which keys are set and whether they come from the site or from application configuration. No key or secret is ever returned.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var settings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<StripeSettings>();
        var configuration = services.GetService<IOptions<StripeConfigurationOptions>>()?.Value ?? new StripeConfigurationOptions();
        var connect = services.GetRequiredService<IStripeConnectService>();

        return Json(new
        {
            mode = (configuration.IsLive ?? settings.IsLive) ? "live" : "test",
            modeFromConfiguration = configuration.IsLive.HasValue,
            live = await DescribeAsync(connect, true, settings.LiveAccountId, settings.LivePublishableKey, settings.LivePrivateSecret, settings.LiveWebhookSecret, configuration.GetKeys(true)),
            test = await DescribeAsync(connect, false, settings.TestAccountId, settings.TestPublishableKey, settings.TestPrivateSecret, settings.TestWebhookSecret, configuration.GetKeys(false)),
            settingsPage = "Settings › Payments › Stripe",
            message = "Connecting an account needs its secret key, which is entered only on the Stripe settings page, never in a chat.",
        });
    }

    private static async Task<object> DescribeAsync(IStripeConnectService connect, bool isLive, string accountId, string publishableKey, string protectedSecret, string protectedWebhookSecret, StripeConfigurationKeys configured)
        => new
        {
            connected = await connect.IsConnectedAsync(isLive),
            accountId,
            hasPublishableKey = configured.HasPublishableKey || !string.IsNullOrEmpty(publishableKey),
            publishableKeyFromConfiguration = configured.HasPublishableKey,
            hasSecretKey = configured.HasSecretKey || !string.IsNullOrEmpty(protectedSecret),
            secretKeyFromConfiguration = configured.HasSecretKey,
            hasWebhookSecret = configured.HasWebhookSecret || !string.IsNullOrEmpty(protectedWebhookSecret),
            webhookSecretFromConfiguration = configured.HasWebhookSecret,
        };
}
