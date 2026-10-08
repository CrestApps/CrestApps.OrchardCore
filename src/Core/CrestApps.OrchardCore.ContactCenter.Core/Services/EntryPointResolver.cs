using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IEntryPointResolver"/>: the call entry point that answers the
/// address a call was dialed to.
/// </summary>
public sealed class EntryPointResolver : IEntryPointResolver
{
    private readonly IContactCenterEntryPointManager _entryPointManager;
    private readonly IOmnichannelChannelEndpointManager _addressManager;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IBusinessHoursService _businessHours;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntryPointResolver"/> class.
    /// </summary>
    /// <param name="entryPointManager">The entry point manager.</param>
    /// <param name="addressManager">The address list entry points pick their numbers from.</param>
    /// <param name="phoneNumberService">The phone number service, to compare a typed number in one form.</param>
    /// <param name="businessHours">The business-hours service used to evaluate open/closed state.</param>
    public EntryPointResolver(
        IContactCenterEntryPointManager entryPointManager,
        IOmnichannelChannelEndpointManager addressManager,
        IPhoneNumberService phoneNumberService,
        IBusinessHoursService businessHours)
    {
        _entryPointManager = entryPointManager;
        _addressManager = addressManager;
        _phoneNumberService = phoneNumberService;
        _businessHours = businessHours;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterEntryPoint> FindByDialedNumberAsync(string dialedNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(dialedNumber))
        {
            return null;
        }

        var entryPoints = (await _entryPointManager.GetEnabledAsync(cancellationToken))
            .Where(entryPoint => string.Equals(entryPoint.GetChannel(), OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // The number the call was dialed to is an address the business owns; the entry point that picked it answers.
        var address = await _addressManager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Phone, dialedNumber, cancellationToken);

        if (address is not null)
        {
            var answering = entryPoints.FirstOrDefault(entryPoint => (entryPoint.AddressIds ?? []).Any(address.IsKnownAs));

            if (answering is not null)
            {
                return answering;
            }
        }

        // An entry point imported from a recipe exported before entry points picked their numbers still lists typed
        // numbers. They are compared in one form, so a number typed with spaces or without its country code matches the
        // call it was meant for, which the exact-text comparison this replaced did not.
        var canonical = Canonicalize(dialedNumber);

        return entryPoints.FirstOrDefault(entryPoint => entryPoint.DialedNumbers is not null &&
            entryPoint.DialedNumbers.Any(number => !string.IsNullOrWhiteSpace(number) &&
                string.Equals(Canonicalize(number), canonical, StringComparison.OrdinalIgnoreCase)));
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

    private string Canonicalize(string number)
        => _phoneNumberService.TryParse(number.Trim(), null, out var canonical) ? canonical.Value : number.Trim();
}
