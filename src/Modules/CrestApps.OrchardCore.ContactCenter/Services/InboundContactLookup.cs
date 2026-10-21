using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IInboundContactLookup"/> implementation that matches contacts by any of their phone numbers,
/// whatever type each is saved as, in E.164 format.
/// </summary>
public sealed class InboundContactLookup : IInboundContactLookup
{
    private readonly IOmnichannelContactPhoneLookup _phoneLookup;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IOmnichannelContactTypeProvider _contactTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundContactLookup"/> class.
    /// </summary>
    /// <param name="phoneLookup">The lookup that finds contacts by phone number.</param>
    /// <param name="phoneNumberService">The phone number service used to normalize numbers to E.164.</param>
    /// <param name="contactTypeProvider">Tells lead types from contact types, so a caller is matched to a contact
    /// before a lead.</param>
    public InboundContactLookup(
        IOmnichannelContactPhoneLookup phoneLookup,
        IPhoneNumberService phoneNumberService,
        IOmnichannelContactTypeProvider contactTypeProvider)
    {
        _phoneLookup = phoneLookup;
        _phoneNumberService = phoneNumberService;
        _contactTypeProvider = contactTypeProvider;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> FindContactItemIdsAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var matches = await FindMatchesAsync(phoneNumber, cancellationToken);

        var ids = new List<string>();

        foreach (var index in matches)
        {
            if (!string.IsNullOrEmpty(index.ContentItemId) && !ids.Contains(index.ContentItemId))
            {
                ids.Add(index.ContentItemId);
            }
        }

        return ids;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> FindCallerItemIdsAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var matches = await FindMatchesAsync(phoneNumber, cancellationToken);

        var leadTypes = await _contactTypeProvider.GetLeadContentTypesAsync(cancellationToken);

        return OmnichannelContactMatches.BestTier(matches, leadTypes);
    }

    private async Task<IReadOnlyList<OmnichannelContactPhoneNumberIndex>> FindMatchesAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return [];
        }

        // The phone number index only ever stores E.164, so a caller identifier that cannot be canonicalized
        // matches nothing. Saying so plainly is better than searching for a digits-only form that the index
        // never contains and reporting the empty result as though the contact were unknown.
        if (!_phoneNumberService.TryParse(phoneNumber, null, out var canonical))
        {
            return [];
        }

        return await _phoneLookup.FindByNumbersAsync([canonical.Value], OmnichannelContactVersion.Published, cancellationToken: cancellationToken);
    }
}
