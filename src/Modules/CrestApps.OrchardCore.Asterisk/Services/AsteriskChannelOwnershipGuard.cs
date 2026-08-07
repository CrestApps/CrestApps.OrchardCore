namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Default implementation of <see cref="IAsteriskChannelOwnershipGuard"/> backed by tenant-scoped channel bindings.
/// </summary>
internal sealed class AsteriskChannelOwnershipGuard : IAsteriskChannelOwnershipGuard
{
    private readonly IAsteriskChannelTenantBindingStore _bindingStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskChannelOwnershipGuard"/> class.
    /// </summary>
    /// <param name="bindingStore">The tenant-scoped channel binding store.</param>
    public AsteriskChannelOwnershipGuard(IAsteriskChannelTenantBindingStore bindingStore)
    {
        _bindingStore = bindingStore;
    }

    /// <inheritdoc/>
    public async Task<bool> IsOwnedByCurrentTenantAsync(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        return await _bindingStore.FindByChannelIdAsync(channelId) is not null;
    }
}
