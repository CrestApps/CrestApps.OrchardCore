using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;

/// <summary>
/// Indexes the delivery log, which sending limits, sending health and bounce tracking are counted from.
/// </summary>
public sealed class EmailDeliveryLogIndex : MapIndex
{
    /// <summary>
    /// The length of the address identifier column.
    /// </summary>
    public const int AddressIdLength = 26;

    /// <summary>
    /// The length of the recipient column, the longest an email address may be.
    /// </summary>
    public const int RecipientLength = 254;

    /// <summary>
    /// The length of the message identifier column.
    /// </summary>
    public const int MessageIdLength = 255;

    /// <summary>
    /// The length of the event identifier column.
    /// </summary>
    public const int EventIdLength = 100;

    /// <summary>
    /// Gets or sets the identifier of the sending address.
    /// </summary>
    public string AddressId { get; set; }

    /// <summary>
    /// Gets or sets what happened, as the numeric value of <see cref="Models.EmailDeliveryEventKind"/>.
    /// </summary>
    public int Kind { get; set; }

    /// <summary>
    /// Gets or sets the recipient.
    /// </summary>
    public string Recipient { get; set; }

    /// <summary>
    /// Gets or sets the recipient's domain.
    /// </summary>
    public string RecipientDomain { get; set; }

    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the provider event identifier.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the email was bulk mail.
    /// </summary>
    public bool IsBulk { get; set; }

    /// <summary>
    /// Gets or sets when it happened.
    /// </summary>
    public DateTime OccurredUtc { get; set; }
}

/// <summary>
/// Indexes the suppression list by address.
/// </summary>
public sealed class EmailSuppressionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the suppressed address.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets why it is suppressed, as the numeric value of <see cref="Models.EmailSuppressionReason"/>.
    /// </summary>
    public int Reason { get; set; }

    /// <summary>
    /// Gets or sets when it was suppressed.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Indexes each address's sending state by address.
/// </summary>
public sealed class EmailSendingStateIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the identifier of the address.
    /// </summary>
    public string AddressId { get; set; }
}
