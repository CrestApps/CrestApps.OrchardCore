namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// What happened to an e-mail handed to <see cref="ITemplatedEmailOutbox"/>.
/// </summary>
public sealed class TemplatedEmailOutboxResult
{
    /// <summary>
    /// Gets the id of the stored e-mail, or <see langword="null"/> when nothing was stored.
    /// </summary>
    public string ItemId { get; init; }

    /// <summary>
    /// Gets what happened to the e-mail.
    /// </summary>
    public TemplatedEmailOutboxStatus Status { get; init; }
}

/// <summary>
/// The states an e-mail in the templated e-mail outbox can be in.
/// </summary>
public enum TemplatedEmailOutboxStatus
{
    /// <summary>
    /// The e-mail service took the e-mail.
    /// </summary>
    Sent,

    /// <summary>
    /// The e-mail service did not take the e-mail yet; a background task retries it.
    /// </summary>
    Queued,

    /// <summary>
    /// The site turned the e-mail's kind off, or the site has no e-mail service; nothing was sent or stored.
    /// </summary>
    Skipped,

    /// <summary>
    /// Every retry failed and the e-mail was given up.
    /// </summary>
    Abandoned,
}
