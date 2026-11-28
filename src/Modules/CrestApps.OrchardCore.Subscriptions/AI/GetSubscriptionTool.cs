using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Returns one subscription agreement as its admin page shows it: the owner, the plan, the amounts and billing period,
/// the lines, the entitlements and the history. The provider's customer id, the checkout session and the guest's email
/// are not returned.
/// </summary>
internal sealed class GetSubscriptionTool : SubscriptionAgreementToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getSubscription";

    private const int MaxEvents = 30;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "subscriptionId": {
          "type": "string",
          "description": "The agreement's id, from listSubscriptions."
        }
      },
      "required": ["subscriptionId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one agreement: owner, plan, amount, billing period, next billing date, lines, entitlements and its history of events. Use it before any change so the user sees what will be affected.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (subscription, error) = await FindAsync(services, ReadString(arguments, "subscriptionId"), cancellationToken);

        if (subscription is null)
        {
            return error;
        }

        var result = await SummarizeAsync(services, subscription);

        result["lines"] = subscription.Lines.Select(line => new
        {
            description = line.Description,
            quantity = line.Quantity,
            unitPrice = line.UnitPrice,
        }).ToList();

        result["entitlements"] = subscription.Entitlements.Select(entitlement => new
        {
            kind = entitlement.Kind,
            value = entitlement.Value,
        }).ToList();

        result["cancellationReason"] = subscription.CancellationReason;
        result["graceEnds"] = await LocalTimeAsync(services, subscription.GraceEndsUtc);

        var events = new List<object>();

        foreach (var entry in subscription.Events.OrderByDescending(entry => entry.CreatedUtc).Take(MaxEvents))
        {
            events.Add(new
            {
                at = await LocalTimeAsync(services, entry.CreatedUtc),
                type = ToCamel(entry.Type.ToString()),
                message = entry.Message,
            });
        }

        result["history"] = events;
        result["historyTruncated"] = subscription.Events.Count > MaxEvents;
        result["adminPath"] = GetAgreementPath(services, subscription);

        return Json(result);
    }
}
