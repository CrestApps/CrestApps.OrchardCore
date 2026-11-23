namespace CrestApps.OrchardCore.Consent.Unsubscribe;

/// <summary>
/// Handles the unsubscribe links of one scope kind on the e-mail preferences page. The module that owns the kind
/// registers it with <c>services.AddUnsubscribeScope&lt;T&gt;()</c>.
/// </summary>
public interface IUnsubscribeScopeHandler
{
    /// <summary>
    /// Gets the scope kind this handler serves, such as <c>MailingList</c>.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Returns the name the preferences page shows for the scope, such as the mailing list's name.
    /// </summary>
    /// <param name="scope">The scope of the link.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The name, or <see langword="null"/> when the scope no longer exists.</returns>
    Task<string> GetTitleAsync(UnsubscribeScope scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops what the link's scope covers for the link's address. Called again for the same link, it changes nothing.
    /// </summary>
    /// <param name="ticket">The address and scope of the link.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    Task UnsubscribeAsync(UnsubscribeTicket ticket, CancellationToken cancellationToken = default);
}
