using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// An email the site sends someone: its kind, who it goes to, the records it's about and the culture it's written in.
/// </summary>
public sealed class TemplatedEmail
{
    /// <summary>
    /// Gets the kind of email, which names the site's template of it, such as <c>ticket-received</c>. A kind is
    /// registered with <c>AddTemplatedEmailKind</c> so the site can write its template. Without a kind, the email is sent
    /// as built, without the site's layout.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets the address the email goes to.
    /// </summary>
    public string To { get; init; }

    /// <summary>
    /// Gets the records the email is about, by the name the site's template reads them under, such as the ticket as
    /// <c>{{ Ticket }}</c>. Each is read as plain values, such as <c>{{ Ticket.Number }}</c>.
    /// </summary>
    public IReadOnlyDictionary<string, object> Models { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the culture the email is written in, such as the recipient's own, or <see langword="null"/> to keep
    /// the current one.
    /// </summary>
    public string Culture { get; init; }

    /// <summary>
    /// Gets what the email is about, such as a ticket number, for the log when it can't be sent.
    /// </summary>
    public string Reference { get; init; }

    /// <summary>
    /// Gets the record the email belongs to, such as <c>Ticket:{id}</c>, for an email sent through
    /// <see cref="ITemplatedEmailOutbox"/>: the outbox keeps it with the email, gives it to its observers, and finds a
    /// record's emails by it (for example to remove them when the person asks to be forgotten).
    /// <see cref="ITemplatedEmailSender"/> does not use it.
    /// </summary>
    public RecordRef Source { get; init; }

    /// <summary>
    /// Gets the sender's own values an email sent through <see cref="ITemplatedEmailOutbox"/> keeps, such as the reply
    /// it carries, given back to the outbox's observers. <see cref="ITemplatedEmailSender"/> does not use them.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
