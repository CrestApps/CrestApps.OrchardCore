using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IDialerProfileReader"/> for a tenant without the dialer. No profile is found, so every
/// caller takes its non-dialer path. Returning an empty profile instead would apply dialer pacing to a tenant
/// that has configured none.
/// </summary>
public sealed class NullDialerProfileReader : IDialerProfileReader
{
    /// <inheritdoc/>
    public ValueTask<DialerProfile> FindByIdAsync(string itemId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<DialerProfile>(null);

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<DialerProfile>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DialerProfile>>([]);
}
