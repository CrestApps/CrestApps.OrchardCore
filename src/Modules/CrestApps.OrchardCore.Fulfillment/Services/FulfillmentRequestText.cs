using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Fulfillment.Services;

/// <summary>
/// Turns a request's status and reason code into localized sentences, for the order card, the list page, the
/// messages after sending and the notifications. Requests store only the code and its values.
/// </summary>
public sealed class FulfillmentRequestText
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentRequestText"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FulfillmentRequestText(IStringLocalizer<FulfillmentRequestText> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the name of a status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The name.</returns>
    public LocalizedString Status(FulfillmentRequestStatus status)
        => status switch
        {
            FulfillmentRequestStatus.Sent => S["Sent"],
            FulfillmentRequestStatus.Sending => S["Sending"],
            FulfillmentRequestStatus.NothingToSend => S["Nothing to send"],
            FulfillmentRequestStatus.NeedsAttention => S["Needs attention"],
            _ => S["Waiting to be sent"],
        };

    /// <summary>
    /// Gets the sentence explaining a request's state.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The sentence.</returns>
    public LocalizedString Reason(FulfillmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var arguments = request.ReasonArguments ?? [];

        string Argument(int index)
            => index < arguments.Count ? arguments[index] : string.Empty;

        return request.Reason switch
        {
            FulfillmentReasons.Sent when !string.IsNullOrEmpty(Argument(0)) => S["Sent. The partner's order is {0}.", Argument(0)],
            FulfillmentReasons.Sent => S["Sent."],
            FulfillmentReasons.Waiting => S["Waiting to be sent."],
            FulfillmentReasons.RetryLater => S["The partner asked to wait, so it is sent again later."],
            FulfillmentReasons.OrderDeleted => S["The order was deleted."],
            FulfillmentReasons.NotPaid => Argument(0) switch
            {
                "Deleted" => S["The order was deleted."], // Requests saved before OrderDeleted had its own reason.
                nameof(OrderStatus.Draft) => S["The order is still a draft."],
                nameof(OrderStatus.AwaitingPayment) => S["The order is awaiting payment."],
                nameof(OrderStatus.Canceled) => S["The order was canceled."],
                _ => S["The order has not been paid."],
            },
            FulfillmentReasons.Refunded => S["The order was refunded."],
            FulfillmentReasons.OpenDispute => S["The order has an open payment dispute."],
            FulfillmentReasons.Anonymized => S["The order's personal details were removed."],
            FulfillmentReasons.Pickup => S["The buyer picks this order up, so there is nothing to ship."],
            FulfillmentReasons.NoAddress => S["The order has no complete shipping address."],
            FulfillmentReasons.Bundle => S["The store ships bundles."],
            FulfillmentReasons.NothingLeft => S["Everything in the order is already in a shipment."],
            FulfillmentReasons.NotClaimed => S["This partner makes none of the items left to ship."],
            FulfillmentReasons.ConflictingClaims => S["\"{0}\" is made by more than one partner ({1}). Link it to one partner only, or ship it yourself, then send again.", Argument(0), Argument(1)],
            FulfillmentReasons.TooMany => S["The items could not be set aside: fewer are left to ship."],
            FulfillmentReasons.AlreadySent => S["Already sent to this partner."],
            FulfillmentReasons.ProviderDisabled => S["The partner {0} is turned off.", Argument(0)],
            FulfillmentReasons.ShippedByHand => S["The items were shipped by hand, so nothing was sent."],
            FulfillmentReasons.Refused => S["The partner refused the order and did not create it."],
            FulfillmentReasons.Unclear => S["The partner did not answer clearly. Check in the partner's dashboard whether it created the order before sending again."],
            FulfillmentReasons.Interrupted => S["Sending was interrupted. Check in the partner's dashboard whether it created the order before sending again."],
            FulfillmentReasons.PartnerNothingToSend => S["The partner had nothing to make in this order."],
            FulfillmentReasons.ClaimFailed => S["The partner could not say which items it makes. Nothing was sent; send again."],
            FulfillmentReasons.GaveUp => S["The partner asked to wait {0} times, so sending stopped. Nothing was sent.", Argument(0)],
            _ => Status(request.Status),
        };
    }

    /// <summary>
    /// Gets whether sending a request again could make the order twice, because the partner may already have it.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when a person should check with the partner first.</returns>
    public static bool MayAlreadyBeMade(FulfillmentRequest request)
        => request?.Status == FulfillmentRequestStatus.NeedsAttention
        && request.Reason is FulfillmentReasons.Unclear or FulfillmentReasons.Interrupted;
}
