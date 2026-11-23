namespace CrestApps.OrchardCore.MailingLists.Core;

/// <summary>
/// The mailing lists settings read from the <c>CrestApps:MailingLists</c> configuration section of the host or a tenant.
/// </summary>
public sealed class MailingListsOptions
{
    /// <summary>
    /// The configuration section the options are read from.
    /// </summary>
    public const string SectionName = "CrestApps:MailingLists";

    /// <summary>
    /// Gets or sets how many member changes the provider outbox sends in one batch.
    /// </summary>
    public int SyncBatchSize { get; set; } = 25;

    /// <summary>
    /// Gets or sets how many times the provider outbox tries a member change before it marks the member "not synced".
    /// </summary>
    public int SyncMaxAttempts { get; set; } = 8;

    /// <summary>
    /// Gets or sets how many days an unconfirmed sign-up is kept before it is deleted.
    /// </summary>
    public int PendingRetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets how many confirmation and claim e-mails one address may be sent for one list within
    /// <see cref="ConfirmationCapMinutes"/>.
    /// </summary>
    public int ConfirmationCap { get; set; } = 3;

    /// <summary>
    /// Gets or sets the window, in minutes, of <see cref="ConfirmationCap"/>.
    /// </summary>
    public int ConfirmationCapMinutes { get; set; } = 15;
}
