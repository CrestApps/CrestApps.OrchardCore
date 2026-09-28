using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Adapts the dialer profile manager to <see cref="IDialerProfileReader"/>, so the feature that owns the catalog
/// is the one that supplies the read side and every other feature depends only on the reader.
/// </summary>
public sealed class DialerProfileManagerReader : IDialerProfileReader
{
    private readonly IDialerProfileManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileManagerReader"/> class.
    /// </summary>
    /// <param name="manager">The dialer profile manager.</param>
    public DialerProfileManagerReader(IDialerProfileManager manager)
    {
        _manager = manager;
    }

    /// <inheritdoc/>
    public ValueTask<DialerProfile> FindByIdAsync(string itemId, CancellationToken cancellationToken = default)
        => _manager.FindByIdAsync(itemId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<DialerProfile>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => _manager.GetEnabledAsync(cancellationToken);
}
