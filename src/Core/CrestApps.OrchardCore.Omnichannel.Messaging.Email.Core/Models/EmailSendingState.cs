namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// Where an email address stands for bulk sending: paused or not, how many times in a row a server has slowed it down,
/// which receiving domains asked it to wait, and the next free turn for held-back mail. Written only when one of those
/// changes, never on an ordinary send.
/// </summary>
public sealed class EmailSendingState
{
    /// <summary>
    /// Gets or sets the record's identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the address.
    /// </summary>
    public string AddressId { get; set; }

    /// <summary>
    /// Gets or sets why bulk sending is paused, or <see cref="EmailSendingPauseKind.None"/>.
    /// </summary>
    public EmailSendingPauseKind PauseKind { get; set; }

    /// <summary>
    /// Gets or sets when the pause ends. <see langword="null"/> for a pause that lasts until someone resumes sending.
    /// </summary>
    public DateTime? PausedUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets what caused the pause, for the address's editor.
    /// </summary>
    public string PauseReason { get; set; }

    /// <summary>
    /// Gets or sets when the pause started.
    /// </summary>
    public DateTime? PausedUtc { get; set; }

    /// <summary>
    /// Gets or sets how many times in a row the sending server asked the address to slow down.
    /// </summary>
    public int ConsecutiveThrottles { get; set; }

    /// <summary>
    /// Gets or sets how many times in a row a receiving system blocked the address.
    /// </summary>
    public int ConsecutiveBlocks { get; set; }

    /// <summary>
    /// Gets or sets the next free turn for held-back bulk mail, so mail held back together is sent spread out at the
    /// address's pace instead of all coming due at the same moment.
    /// </summary>
    public DateTime? NextBulkSlotUtc { get; set; }

    /// <summary>
    /// Gets or sets the receiving domains that asked the address to wait, with when the wait ends.
    /// </summary>
    public Dictionary<string, DateTime> DomainBackoffs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets when the record last changed.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets a value indicating whether bulk sending is paused at <paramref name="utcNow"/>.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    /// <returns><see langword="true"/> while paused.</returns>
    public bool IsPaused(DateTime utcNow)
        => PauseKind != EmailSendingPauseKind.None && (PausedUntilUtc is null || PausedUntilUtc > utcNow);
}

/// <summary>
/// Why an address's bulk sending is paused.
/// </summary>
public enum EmailSendingPauseKind
{
    /// <summary>
    /// Not paused.
    /// </summary>
    None = 0,

    /// <summary>
    /// The sending server asked the address to slow down. The pause ends by itself, longer each time it repeats.
    /// </summary>
    Throttled = 1,

    /// <summary>
    /// A receiving system refused the address's mail for policy or reputation. The pause ends by itself, longer each
    /// time it repeats.
    /// </summary>
    Blocked = 2,

    /// <summary>
    /// The address's bounce or complaint rate passed the level that gets senders blocked. It lasts until someone
    /// resumes sending, after cleaning the list.
    /// </summary>
    PoorHealth = 3,
}
