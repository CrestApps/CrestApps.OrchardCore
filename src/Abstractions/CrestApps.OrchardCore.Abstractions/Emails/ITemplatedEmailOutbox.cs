namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// Sends a templated e-mail that must not be lost: the e-mail is rendered (with the site's template for its kind),
/// kept in the caller's unit of work and sent; when the e-mail service does not take it, a background task retries it
/// with backoff until it is sent or given up. Use it for e-mails a person waits for (a confirmation link, a requester
/// notice); use <see cref="ITemplatedEmailSender"/> for e-mails that may be skipped.
/// </summary>
/// <remarks>
/// The first attempt happens during <see cref="SendAsync"/>, before the caller's unit of work commits: when the caller
/// then rolls back, an e-mail sent at once has gone while nothing about it is kept, and one that waits is never tried
/// again. Call it once the change the e-mail is about is sure to commit (for example from an observer that runs after
/// the commit), or accept that a rolled-back change may still have sent its e-mail. Observers
/// (<see cref="ITemplatedEmailOutboxObserver"/>) hear of every attempt, the first one included.
/// </remarks>
public interface ITemplatedEmailOutbox
{
    /// <summary>
    /// Renders, stores and sends the e-mail.
    /// </summary>
    /// <param name="email">The kind, recipient, models and culture of the e-mail.</param>
    /// <param name="buildAsync">Builds the module's own content of the e-mail; the site's template for the kind wraps it.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>Whether the e-mail left now or waits for a retry, and the id of the stored e-mail.</returns>
    Task<TemplatedEmailOutboxResult> SendAsync(TemplatedEmail email, Func<CancellationToken, Task<TemplatedEmailContent>> buildAsync, CancellationToken cancellationToken = default);
}
