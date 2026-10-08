using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Converts a lead with nobody at the screen: the Convert Lead subject action, the Convert Lead workflow task and the
/// AI all go through here, so they share one rule for choosing the contact.
/// </summary>
internal sealed class UnattendedLeadConverter : IUnattendedLeadConverter
{
    private readonly ILeadConversionService _conversionService;
    private readonly LeadMatchFinder _matchFinder;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnattendedLeadConverter"/> class.
    /// </summary>
    /// <param name="conversionService">The lead conversion service.</param>
    /// <param name="matchFinder">Finds the contacts that share the lead's phone number or email.</param>
    public UnattendedLeadConverter(
        ILeadConversionService conversionService,
        LeadMatchFinder matchFinder)
    {
        _conversionService = conversionService;
        _matchFinder = matchFinder;
    }

    /// <inheritdoc/>
    public async Task<LeadConversionResult> ConvertAsync(ContentItem lead, LeadConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(lead);
        ArgumentNullException.ThrowIfNull(request);

        // With more than one matching contact there is no telling which is this person, so a new contact is created
        // rather than merging into the wrong customer.
        var matches = await _matchFinder.FindContactsAsync(lead);

        request.LeadContentItemId = lead.ContentItemId;
        request.ExistingContactItemId = matches.Count == 1 ? matches[0].ContentItemId : null;

        return await _conversionService.ConvertAsync(request);
    }
}
