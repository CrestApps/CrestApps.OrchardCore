using CrestApps.Core.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// An email kept by the templated email outbox: rendered once, with the site's template of its kind, and sent as it
/// was rendered on every attempt. It is an item of a leased work queue, so one node at a time retries it.
/// </summary>
public sealed class TemplatedEmailOutboxItem : CatalogItem, ILeasedWorkItem
{
    /// <summary>
    /// Gets or sets the kind of the email, the name of a templated email kind, or <see langword="null"/>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the address the email goes to.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the culture the email was written in, or <see langword="null"/>.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets what the email is about, for the log.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets the record the email belongs to.
    /// </summary>
    public RecordRef Source { get; set; }

    /// <summary>
    /// Gets or sets the sender's own values the email keeps (<see cref="CrestApps.OrchardCore.Emails.TemplatedEmail.Properties"/>), given back
    /// to the outbox's observers.
    /// </summary>
    public Dictionary<string, string> EmailProperties { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the subject, as the site's template wrote it.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the text body, as the site's template wrote it, or <see langword="null"/>.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the HTML body, as the site's template wrote it, or <see langword="null"/>.
    /// </summary>
    public string Html { get; set; }

    /// <summary>
    /// Gets or sets where the email stands on the queue: pending or claimed while it waits, completed once sent,
    /// dead-lettered once given up.
    /// </summary>
    public LeasedWorkItemStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the token of the worker that holds the current claim.
    /// </summary>
    public string OwnerToken { get; set; }

    /// <summary>
    /// Gets or sets the fence token of the current claim.
    /// </summary>
    public long FenceToken { get; set; }

    /// <summary>
    /// Gets or sets how many attempts failed.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the next attempt while the email waits, or the lease expiry while it is claimed.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets why the last attempt failed.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the email was handed to the outbox.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the email service took the email, or <see langword="null"/>.
    /// </summary>
    public DateTime? SentUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the email was given up, or <see langword="null"/>.
    /// </summary>
    public DateTime? AbandonedUtc { get; set; }
}
