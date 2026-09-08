using System.Text.Json.Nodes;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The read/write surface of a checkout session that the flow navigation and step drivers operate on.
/// </summary>
public interface ICheckoutFlowSession : IEntity
{
    /// <summary>
    /// The unique identifier of the checkout session.
    /// </summary>
    string SessionId { get; set; }

    /// <summary>
    /// The key of the step the customer is currently on.
    /// </summary>
    string CurrentStep { get; set; }

    /// <summary>
    /// The lifecycle state of the checkout.
    /// </summary>
    CheckoutSessionStatus Status { get; set; }

    /// <summary>
    /// The currency this checkout is priced and charged in.
    /// </summary>
    /// <remarks>
    /// It is set from what is being bought rather than from a site setting, so an invoice is never built in
    /// one currency from line items priced in another.
    /// </remarks>
    string Currency { get; set; }

    /// <summary>
    /// The ordered steps that make up this checkout.
    /// </summary>
    IList<CheckoutFlowStep> Steps { get; }

    /// <summary>
    /// The data collected for each completed step, keyed by step key.
    /// </summary>
    JsonObject SavedSteps { get; }

    /// <summary>
    /// The identifier of the authenticated user that owns the session, when applicable.
    /// </summary>
    string OwnerId { get; set; }
}
