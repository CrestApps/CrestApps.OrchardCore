using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Pauses billing on an agreement or resumes it, through the lifecycle service the agreement's page uses
/// (<see cref="ISubscriptionLifecycleService.PauseAsync"/> and <see cref="ISubscriptionLifecycleService.ResumeAsync"/>).
/// Both return without an error when nothing changed, and the page reports success anyway; the tool checks the status
/// first and says when there is nothing to do.
/// </summary>
internal sealed class SetSubscriptionPausedTool : SubscriptionAgreementToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setSubscriptionPaused";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "subscriptionId": {
          "type": "string",
          "description": "The agreement's id, from listSubscriptions."
        },
        "paused": {
          "type": "boolean",
          "description": "true to suspend billing; false to resume a paused or past-due agreement, so the customer is billed again."
        },
        "reason": {
          "type": "string",
          "description": "Why billing is paused, kept in the agreement's history. Not used when resuming."
        }
      },
      "required": ["subscriptionId", "paused"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Pauses billing on an agreement (with a reason) or resumes a paused or past-due one. The change is sent to the payment provider, so resuming means the customer is billed again.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadBool(arguments, "paused") is not { } paused)
        {
            return "Nothing was changed: set paused to true to suspend billing or to false to resume it.";
        }

        var (subscription, error) = await FindAsync(services, ReadString(arguments, "subscriptionId"), cancellationToken);

        if (subscription is null)
        {
            return error;
        }

        var before = subscription.Status;

        if (paused && before is SubscriptionStatus.Canceled or SubscriptionStatus.Expired or SubscriptionStatus.Paused)
        {
            return $"Nothing was changed: agreement '{subscription.Title}' is {ToCamel(before.ToString())}, so billing can't be paused.";
        }

        if (!paused && before is not (SubscriptionStatus.Paused or SubscriptionStatus.PastDue))
        {
            return $"Nothing was changed: agreement '{subscription.Title}' is {ToCamel(before.ToString())}; only a paused or past-due agreement can be resumed.";
        }

        var lifecycle = services.GetRequiredService<ISubscriptionLifecycleService>();

        var changed = paused
            ? await lifecycle.PauseAsync(subscription.ItemId, ReadString(arguments, "reason") ?? "Paused by an administrator.", cancellationToken)
            : await lifecycle.ResumeAsync(subscription.ItemId, cancellationToken);

        if (await CommitAsync(services, cancellationToken) is { } saveError)
        {
            return saveError;
        }

        changed ??= subscription;

        var expected = paused ? SubscriptionStatus.Paused : SubscriptionStatus.Active;

        if (changed.Status != expected)
        {
            return $"Nothing was changed: agreement '{subscription.Title}' is {ToCamel(changed.Status.ToString())}. Read it again with getSubscription.";
        }

        var result = await SummarizeAsync(services, changed);

        result["previousStatus"] = ToCamel(before.ToString());
        result["message"] = paused
            ? $"Billing on agreement '{changed.Title}' is suspended."
            : $"Agreement '{changed.Title}' is active again and will be billed from its next billing date.";
        result["provider"] = DescribeProvider(changed);
        result["adminPath"] = GetAgreementPath(services, changed);

        return Json(result);
    }
}
