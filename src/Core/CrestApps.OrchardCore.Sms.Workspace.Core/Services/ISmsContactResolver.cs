namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// Resolves the Omnichannel contact content item for a contact phone number, so a conversation can link to
/// the CRM contact. The default implementation resolves nothing (an "unknown contact"); a richer
/// implementation queries the phone-match indexes.
/// </summary>
public interface ISmsContactResolver
{
    /// <summary>
    /// Resolves the content item id of the contact that owns the specified phone number.
    /// </summary>
    /// <param name="phoneNumber">The contact phone number (E.164).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The contact content item id, or <see langword="null"/> when unknown.</returns>
    ValueTask<string> ResolveContactContentItemIdAsync(string phoneNumber, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default no-op contact resolver: every inbound conversation starts as an "unknown contact" until a
/// phone-match resolver is registered.
/// </summary>
public sealed class NullSmsContactResolver : ISmsContactResolver
{
    /// <inheritdoc/>
    public ValueTask<string> ResolveContactContentItemIdAsync(string phoneNumber, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<string>(null);
}
