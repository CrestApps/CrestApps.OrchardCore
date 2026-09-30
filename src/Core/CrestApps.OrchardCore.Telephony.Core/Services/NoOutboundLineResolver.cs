using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// The default <see cref="IOutboundLineResolver"/> for a deployment that does not manage its numbers: nobody has a
/// line of their own, so every call presents the provider's default caller ID.
/// </summary>
public sealed class NoOutboundLineResolver : IOutboundLineResolver
{
    /// <inheritdoc/>
    public Task<OutboundLine> ResolveAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<OutboundLine>(null);
}
