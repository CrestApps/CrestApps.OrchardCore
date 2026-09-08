using System.Text.Json.Nodes;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// A persisted checkout session. It is the provider-agnostic unit of work for any purchase flow
/// (subscriptions, one-time goods, ...). Its property bag carries the <see cref="CheckoutInvoice"/>,
/// provider metadata, and <see cref="PaymentsMetadata"/>.
/// </summary>
public sealed class CheckoutSession : Entity, ICheckoutFlowSession
{
    /// <summary>
    /// The YesSql document identity.
    /// </summary>
    /// <remarks>
    /// It matters more than it looks. YesSql recognises a document it has already stored by this property;
    /// without one, saving a session again after a commit inserts a second document rather than updating the
    /// first, and the checkout then exists twice — once pending and once completed.
    /// </remarks>
    public long Id { get; set; }

    /// <summary>
    /// The unique identifier of the checkout session.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The kind of thing being purchased (for example a content type or an application-defined reference type).
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// The identifier of the thing being purchased.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// An optional secondary identifier of the thing being purchased (for example a content item version id).
    /// </summary>
    public string ReferenceVersionId { get; set; }

    /// <summary>
    /// The ISO-4217 currency code for the checkout.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The lifecycle state of the checkout.
    /// </summary>
    public CheckoutSessionStatus Status { get; set; }

    /// <summary>
    /// The UTC time the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC time the session was last modified.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// The UTC time the session completed, when applicable.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// The identifier of the authenticated user that owns the session, when applicable.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// The data collected for each completed step, keyed by step key.
    /// </summary>
    public JsonObject SavedSteps { get; init; } = [];

    /// <summary>
    /// The ordered steps that make up this checkout.
    /// </summary>
    public IList<CheckoutFlowStep> Steps { get; init; } = [];

    /// <summary>
    /// The key of the step the customer is currently on.
    /// </summary>
    public string CurrentStep { get; set; }

    /// <summary>
    /// The client IP address captured for anonymous sessions. It is recorded for audit only and must never
    /// be used to decide who may resume a session: everyone behind one router shares it.
    /// </summary>
    public string IPAddress { get; set; }

    /// <summary>
    /// The user agent captured for anonymous sessions. It is recorded for audit only and must never be used
    /// to decide who may resume a session: it is neither secret nor unique.
    /// </summary>
    public string AgentInfo { get; set; }

    /// <summary>
    /// The SHA-256 hash of the ownership token issued to the guest who started this session. Only that
    /// browser holds the token, so it is what actually proves ownership. Only the hash is stored, so a
    /// leaked database does not hand an attacker the ability to resume live checkouts.
    /// </summary>
    public string GuestTokenHash { get; set; }
}
