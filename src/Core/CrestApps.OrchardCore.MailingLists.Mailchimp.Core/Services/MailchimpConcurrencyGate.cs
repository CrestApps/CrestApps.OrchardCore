namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Keeps the number of Mailchimp calls a tenant makes at the same time low. Mailchimp allows 10 simultaneous
/// connections per Mailchimp user across every client of the account, and answers 429 past that. Registered as a
/// singleton of the tenant's container, so each tenant has its own gate.
/// </summary>
public sealed class MailchimpConcurrencyGate : IDisposable
{
    /// <summary>
    /// The most Mailchimp calls one tenant makes at the same time.
    /// </summary>
    public const int MaxConcurrentCalls = 3;

    private readonly SemaphoreSlim _semaphore = new(MaxConcurrentCalls, MaxConcurrentCalls);

    /// <summary>
    /// Waits for a free slot.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the wait.</param>
    public Task WaitAsync(CancellationToken cancellationToken)
        => _semaphore.WaitAsync(cancellationToken);

    /// <summary>
    /// Frees the slot taken by <see cref="WaitAsync"/>.
    /// </summary>
    public void Release()
        => _semaphore.Release();

    /// <inheritdoc/>
    public void Dispose()
        => _semaphore.Dispose();
}
