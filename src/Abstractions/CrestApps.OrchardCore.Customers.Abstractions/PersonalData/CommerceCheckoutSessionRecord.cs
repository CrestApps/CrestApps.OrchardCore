using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// A checkout session of the person, with what it recorded about them.
/// </summary>
public sealed class CommerceCheckoutSessionRecord
{
    /// <summary>
    /// Gets or sets the checkout session identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the kind of thing being bought.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the thing being bought.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the status of the checkout.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency of the checkout.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the user the checkout belongs to, or <see langword="null"/> for a guest.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets when the checkout started.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the checkout completed, when it did.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the name the buyer gave.
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the email the buyer gave.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the phone number the buyer gave.
    /// </summary>
    public string ContactPhone { get; set; }

    /// <summary>
    /// Gets or sets the IP address recorded for a guest checkout.
    /// </summary>
    public string IPAddress { get; set; }

    /// <summary>
    /// Gets or sets the user agent recorded for a guest checkout.
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the data the buyer entered in each step, keyed by step.
    /// </summary>
    public JsonObject SavedSteps { get; set; }
}
