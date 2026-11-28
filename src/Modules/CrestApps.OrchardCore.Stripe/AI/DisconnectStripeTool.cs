using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Stripe.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Stripe.AI;

/// <summary>
/// Disconnects the Stripe account of the live or the test environment through the Disconnect action of
/// Settings › Payments › Stripe (<see cref="IStripeConnectService.DisconnectAsync"/>): it removes the webhook at Stripe
/// and clears the stored keys of that environment. Keys from the application configuration stay in use. Connecting
/// again is done on the settings page, which takes the secret key.
/// </summary>
internal sealed class DisconnectStripeTool : StripeAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "disconnectStripe";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "environment": {
          "type": "string",
          "enum": ["live", "test"],
          "description": "Which Stripe environment to disconnect."
        }
      },
      "required": ["environment"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Disconnects the Stripe account of the live or the test environment: removes the webhook at Stripe and clears the stored keys for it. Checkout can no longer take card payments in that environment until an administrator connects again on the Stripe settings page.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!TryReadEnvironment(arguments, out var isLive))
        {
            return "Nothing was done: environment must be live or test.";
        }

        var result = await services.GetRequiredService<IStripeConnectService>().DisconnectAsync(isLive);

        if (!result.Succeeded)
        {
            return $"Nothing was done: {result.Message}";
        }

        return Json(new
        {
            environment = isLive ? "live" : "test",
            disconnected = true,
            accountId = result.AccountId,
            message = $"{result.Message} To connect again, an administrator enters the secret key on Settings › Payments › Stripe.",
        });
    }
}
