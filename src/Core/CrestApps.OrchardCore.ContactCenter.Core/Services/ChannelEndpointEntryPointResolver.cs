using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Resolves the entry point chosen on a phone number's own channel endpoint, so a number is routed from the screen it
/// is managed on, as an SMS number is. It is asked before the entry points' typed-in dialed numbers: the number's own
/// setting is the more specific of the two.
/// </summary>
public sealed class ChannelEndpointEntryPointResolver : IEntryPointResolver, IOrderedEntryPointResolver
{
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IContactCenterEntryPointManager _entryPointManager;
    private readonly IBusinessHoursService _businessHours;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelEndpointEntryPointResolver"/> class.
    /// </summary>
    /// <param name="endpointManager">The channel endpoint catalog holding the tenant's numbers.</param>
    /// <param name="entryPointManager">The entry point manager.</param>
    /// <param name="businessHours">The business-hours service used to evaluate open/closed state.</param>
    public ChannelEndpointEntryPointResolver(
        IOmnichannelChannelEndpointManager endpointManager,
        IContactCenterEntryPointManager entryPointManager,
        IBusinessHoursService businessHours)
    {
        _endpointManager = endpointManager;
        _entryPointManager = entryPointManager;
        _businessHours = businessHours;
    }

    /// <inheritdoc/>
    public int Order => -100;

    /// <inheritdoc/>
    public async Task<ContactCenterEntryPoint> FindByDialedNumberAsync(string dialedNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dialedNumber))
        {
            return null;
        }

        var endpoint = await _endpointManager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Phone, dialedNumber, cancellationToken);
        var entryPointId = endpoint?.GetOrCreate<PhoneEndpointRoutingSettings>().EntryPointId;

        if (string.IsNullOrEmpty(entryPointId))
        {
            return null;
        }

        var entryPoint = await _entryPointManager.FindByIdAsync(entryPointId, cancellationToken);

        // A disabled or deleted entry point leaves the number to the rest of the chain, as though none were chosen.
        return entryPoint is { Enabled: true } ? entryPoint : null;
    }

    /// <inheritdoc/>
    public async Task<EntryPointRoutingPlan> ResolveAsync(string dialedNumber, CancellationToken cancellationToken = default)
    {
        var entryPoint = await FindByDialedNumberAsync(dialedNumber, cancellationToken);

        if (entryPoint is null)
        {
            return null;
        }

        var isOpen = await _businessHours.IsOpenAsync(entryPoint.BusinessHoursCalendarId, cancellationToken);

        return EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen);
    }
}
