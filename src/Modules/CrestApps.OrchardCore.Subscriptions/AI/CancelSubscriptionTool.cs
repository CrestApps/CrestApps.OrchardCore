using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Cancels an agreement now or at the end of the paid period through the lifecycle service the agreement's page uses
/// (<see cref="ISubscriptionLifecycleService.CancelAsync"/>), with the caller's user id as the source, as the page
/// passes it. Canceling never refunds: the result says so and points to the order refund. The service does nothing to
/// an expired agreement or one already canceled the same way; the tool compares the agreement before and after and says
/// when nothing changed.
/// </summary>
internal sealed class CancelSubscriptionTool : SubscriptionAgreementToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cancelSubscription";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "subscriptionId": {
          "type": "string",
          "description": "The agreement's id, from listSubscriptions."
        },
        "atPeriodEnd": {
          "type": "boolean",
          "description": "true to keep access until the end of the period already paid for; false to end it now."
        },
        "reason": {
          "type": "string",
          "description": "Why it is canceled, kept in the agreement's history."
        }
      },
      "required": ["subscriptionId", "atPeriodEnd"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Cancels an agreement now or at the end of the paid period and tells the payment provider to stop billing. Cannot be undone; a canceled agreement is not restarted, the customer must subscribe again. It never refunds.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadBool(arguments, "atPeriodEnd") is not { } atPeriodEnd)
        {
            return "Nothing was canceled: say whether access ends now (atPeriodEnd false) or at the end of the paid period (atPeriodEnd true).";
        }

        var (subscription, error) = await FindAsync(services, ReadString(arguments, "subscriptionId"), cancellationToken);

        if (subscription is null)
        {
            return error;
        }

        if (subscription.Status == SubscriptionStatus.Expired)
        {
            return $"Nothing was canceled: agreement '{subscription.Title}' has already expired.";
        }

        if (subscription.Status == SubscriptionStatus.Canceled && subscription.CancelAtPeriodEnd == atPeriodEnd)
        {
            return $"Nothing was changed: agreement '{subscription.Title}' is already canceled that way.";
        }

        var reason = ReadString(arguments, "reason") ?? "Canceled by an administrator.";

        var changed = await services.GetRequiredService<ISubscriptionLifecycleService>()
            .CancelAsync(subscription.ItemId, atPeriodEnd, reason, GetUserId(caller), cancellationToken);

        if (await CommitAsync(services, cancellationToken) is { } saveError)
        {
            return saveError;
        }

        changed ??= subscription;

        if (changed.Status != SubscriptionStatus.Canceled)
        {
            return $"Nothing was canceled: agreement '{subscription.Title}' is {ToCamel(changed.Status.ToString())} and the cancellation was not applied. Read it again with getSubscription.";
        }

        var result = await SummarizeAsync(services, changed);

        result["message"] = atPeriodEnd
            ? $"Agreement '{changed.Title}' was canceled; access runs to the end of the paid period. Nothing more will be billed."
            : $"Agreement '{changed.Title}' was canceled and access ended now. Nothing more will be billed.";
        result["refunded"] = false;
        result["refundNote"] = "Nothing was refunded. To give money back for a period already paid, refund the order it was paid with (refundOrder).";
        result["provider"] = DescribeProvider(changed);
        result["adminPath"] = GetAgreementPath(services, changed);

        return Json(result);
    }
}
