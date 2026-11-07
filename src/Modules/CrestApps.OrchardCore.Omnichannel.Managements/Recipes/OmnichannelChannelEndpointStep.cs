using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Recipes;

/// <summary>
/// Imports Omnichannel channel endpoints carried by a recipe step, creating entries that do not exist and updating
/// those that do.
/// </summary>
internal sealed class OmnichannelChannelEndpointStep : CatalogRecipeStep<OmnichannelChannelEndpoint>
{
    private readonly IOmnichannelChannelEndpointManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the channel endpoints.</param>
    public OmnichannelChannelEndpointStep(IOmnichannelChannelEndpointManager manager)
        : base(OmnichannelDeploymentSteps.ChannelEndpoint, "ChannelEndpoints", manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// A recipe exported before addresses had capabilities lists a number once per channel. The second record joins
    /// the address the first created instead of being refused as a duplicate.
    /// </summary>
    /// <param name="entry">The new, unsaved entry.</param>
    /// <returns><see langword="true"/> when the entry joined a stored address.</returns>
    protected override async ValueTask<bool> TryMergeNewEntryAsync(OmnichannelChannelEndpoint entry)
    {
        var existing = await FindSameAddressAsync(entry);

        if (existing is null)
        {
            return false;
        }

        OmnichannelAddressConsolidator.Absorb(existing, entry);
        await _manager.UpdateAsync(existing);

        return true;
    }

    private async Task<OmnichannelChannelEndpoint> FindSameAddressAsync(OmnichannelChannelEndpoint entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Value))
        {
            return null;
        }

        var addressType = entry.GetAddressType();

        return (await _manager.GetAllAsync()).FirstOrDefault(address =>
            string.Equals(address.GetAddressType(), addressType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(address.Value, entry.Value, StringComparison.OrdinalIgnoreCase));
    }
}
