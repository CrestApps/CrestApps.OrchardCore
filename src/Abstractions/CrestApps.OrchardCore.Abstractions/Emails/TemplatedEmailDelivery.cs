using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// An attempt to send an email kept by <see cref="ITemplatedEmailOutbox"/>, as its observers see it.
/// </summary>
public sealed class TemplatedEmailDelivery
{
    /// <summary>
    /// Gets the id of the stored email, the one <see cref="TemplatedEmailOutboxResult.ItemId"/> gave.
    /// </summary>
    public string ItemId { get; init; }

    /// <summary>
    /// Gets the kind of the email, the name of a <see cref="TemplatedEmailKind"/>, or <see langword="null"/>.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets the address the email goes to.
    /// </summary>
    public string To { get; init; }

    /// <summary>
    /// Gets what the email is about, for the log (<see cref="TemplatedEmail.Reference"/>).
    /// </summary>
    public string Reference { get; init; }

    /// <summary>
    /// Gets the record the email belongs to (<see cref="TemplatedEmail.Source"/>).
    /// </summary>
    public RecordRef Source { get; init; }

    /// <summary>
    /// Gets the sender's own values the email keeps (<see cref="TemplatedEmail.Properties"/>).
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets where the email stands after the attempt: <see cref="TemplatedEmailOutboxStatus.Sent"/>,
    /// <see cref="TemplatedEmailOutboxStatus.Queued"/> (tried again later) or
    /// <see cref="TemplatedEmailOutboxStatus.Abandoned"/>.
    /// </summary>
    public TemplatedEmailOutboxStatus Status { get; init; }

    /// <summary>
    /// Gets how many attempts failed so far.
    /// </summary>
    public int FailedAttempts { get; init; }

    /// <summary>
    /// Gets why the last attempt failed, or <see langword="null"/> when the email was sent.
    /// </summary>
    public string LastError { get; init; }

    /// <summary>
    /// Gets the UTC time of the next attempt of an email that waits for one, or <see langword="null"/>.
    /// </summary>
    public DateTime? NextAttemptUtc { get; init; }
}
