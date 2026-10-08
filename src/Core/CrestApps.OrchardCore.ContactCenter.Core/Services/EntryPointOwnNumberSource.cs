using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Names the numbers customers dial to reach the contact center: every number an entry point answers, enabled or
/// not, because a disabled entry point's number still rings the tenant's own lines.
/// </summary>
public sealed class EntryPointOwnNumberSource : IContactCenterOwnNumberSource
{
    private readonly IContactCenterEntryPointManager _entryPointManager;
    private readonly IOmnichannelChannelEndpointManager _addressManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntryPointOwnNumberSource"/> class.
    /// </summary>
    /// <param name="entryPointManager">The entry point catalog.</param>
    /// <param name="addressManager">The address list entry points pick their numbers from.</param>
    public EntryPointOwnNumberSource(
        IContactCenterEntryPointManager entryPointManager,
        IOmnichannelChannelEndpointManager addressManager)
    {
        _entryPointManager = entryPointManager;
        _addressManager = addressManager;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetOwnNumbersAsync(CancellationToken cancellationToken = default)
    {
        var entryPoints = (await _entryPointManager.GetAllAsync(cancellationToken))
            .Where(entryPoint => entryPoint is not null)
            .ToList();
        var addresses = await _addressManager.GetAllAsync(cancellationToken);

        var picked = entryPoints
            .SelectMany(entryPoint => entryPoint.AddressIds ?? [])
            .Select(id => addresses.FirstOrDefault(address => address.IsKnownAs(id))?.Value);

        // A number still typed on an entry point imported from an older recipe rings the tenant's lines too.
        var typed = entryPoints.SelectMany(entryPoint => entryPoint.DialedNumbers ?? []);

        return picked
            .Concat(typed)
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
