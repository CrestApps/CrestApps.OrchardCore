namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// Hears what happens to the emails kept by <see cref="ITemplatedEmailOutbox"/>, so a module can show it on its own
/// records, such as a "not delivered" mark on the reply an email carried. Register one with
/// <c>AddTemplatedEmailOutboxObserver</c>. Each callback runs after every attempt, the first one (inside
/// <see cref="ITemplatedEmailOutbox.SendAsync"/>, in the caller's unit of work) and each retry (in the background
/// task's scope); an observer that fails is logged and never stops the outbox. An observer only reacts to the emails
/// it knows, by their <see cref="TemplatedEmailDelivery.Source"/>, <see cref="TemplatedEmailDelivery.Kind"/> or
/// <see cref="TemplatedEmailDelivery.Properties"/>.
/// </summary>
public interface ITemplatedEmailOutboxObserver
{
    /// <summary>
    /// Runs when the email service took the email.
    /// </summary>
    /// <param name="delivery">The email and the attempt.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    Task DeliveredAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs when an attempt failed and the email waits for the next one.
    /// </summary>
    /// <param name="delivery">The email, the attempt and when the next one is due.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    Task RetryingAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs when the last attempt failed and the email was given up.
    /// </summary>
    /// <param name="delivery">The email and the last attempt.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    Task AbandonedAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default);
}
