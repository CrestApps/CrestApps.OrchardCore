using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Puts a gateway's dispute reasons and statuses, such as <c>product_not_received</c> or <c>needs_response</c>, into
/// words; one it doesn't know is shown as it came.
/// </summary>
public static class OrderDisputeText
{
    public static string Reason(IStringLocalizer S, string reason)
        => reason switch
        {
            "fraudulent" => S["they don't recognize the payment"],
            "product_not_received" => S["they say it never arrived"],
            "product_unacceptable" => S["they say it wasn't as described"],
            "duplicate" => S["they say they were charged twice"],
            "subscription_canceled" => S["they say they had canceled"],
            "credit_not_processed" => S["they say a refund never came"],
            "unrecognized" => S["they don't recognize the payment"],
            "general" or null or "" => S["no reason given"],
            _ => reason.Replace('_', ' '),
        };

    public static string Status(IStringLocalizer S, string status)
        => status switch
        {
            "warning_needs_response" or "needs_response" => S["needs your response"],
            "warning_under_review" or "under_review" => S["your response is under review"],
            "warning_closed" => S["closed without a chargeback"],
            "won" => S["won, the money stays with you"],
            "lost" => S["lost, the money went back to the payer"],
            null or "" => S["open"],
            _ => status.Replace('_', ' '),
        };
}
