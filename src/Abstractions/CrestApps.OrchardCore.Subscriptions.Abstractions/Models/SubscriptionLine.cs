namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// One priced line of what a <see cref="Subscription"/> bills each cycle.
/// </summary>
public sealed class SubscriptionLine
{
    /// <summary>
    /// Gets or sets the identifier of the line, usually the product or plan it came from.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the description the customer sees on an invoice.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the quantity billed.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the price of one unit, in major currency units.
    /// </summary>
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// One thing that happened to a <see cref="Subscription"/>, kept so the history of an agreement can be read
/// back later.
/// </summary>
public sealed class SubscriptionEvent
{
    /// <summary>
    /// Gets or sets the UTC time the event happened.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public SubscriptionEventType Type { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets who or what caused it, for example an operator's user id or a provider key.
    /// </summary>
    public string Source { get; set; }
}

/// <summary>
/// The kind of thing recorded in a <see cref="SubscriptionEvent"/>.
/// </summary>
public enum SubscriptionEventType
{
    /// <summary>
    /// The agreement was created.
    /// </summary>
    Created = 0,

    /// <summary>
    /// A cycle was billed and paid.
    /// </summary>
    Renewed = 1,

    /// <summary>
    /// A renewal payment failed.
    /// </summary>
    PaymentFailed = 2,

    /// <summary>
    /// The agreement was canceled.
    /// </summary>
    Canceled = 3,

    /// <summary>
    /// The agreement ended.
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Billing was suspended.
    /// </summary>
    Paused = 5,

    /// <summary>
    /// Billing was resumed.
    /// </summary>
    Resumed = 6,

    /// <summary>
    /// What the agreement bills was changed.
    /// </summary>
    Changed = 7,

    /// <summary>
    /// A note that does not change the state.
    /// </summary>
    Note = 8,
}

/// <summary>
/// Something a <see cref="Subscription"/> grants its owner while it is current.
/// </summary>
/// <remarks>
/// This is what lets a subscription mean something to the rest of the site rather than only to the billing
/// ledger: a role to hold, a tenant to run, a content item to reach. The subscription decides whether the
/// entitlement is current; a feature that understands the kind decides what to do about it.
/// </remarks>
public sealed class SubscriptionEntitlement
{
    /// <summary>
    /// Gets or sets the kind of entitlement, which selects the feature that applies it.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the value the kind interprets, for example a role name or a tenant name.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets extra data the applying feature needs.
    /// </summary>
    public Dictionary<string, string> Data { get; set; }
}
