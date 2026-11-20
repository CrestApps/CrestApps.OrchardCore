using System.Globalization;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Bills a CRM contact (party kind <see cref="PartyKinds.Contact"/>) under its name, at its first e-mail address, with
/// the number it is called on (else its first number) when that is in E.164 form, and at the first address an address
/// field on its content type holds, the first in editor order (part position, then field position). The published version of the contact is read, or its latest version while it has
/// never been published. A contact has no members: nobody signs in for it.
/// </summary>
internal sealed class ContactBillingProfileSource : IPartyBillingProfileSource
{
    private const string AddressFieldName = nameof(AddressField);

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContactMethodsAccessor _contactMethodsAccessor;

    public ContactBillingProfileSource(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IContactMethodsAccessor contactMethodsAccessor)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _contactMethodsAccessor = contactMethodsAccessor;
    }

    public string Kind
        => PartyKinds.Contact;

    public async Task ContributeAsync(PartyRef party, PartyBillingProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (party.IsEmpty)
        {
            return;
        }

        var contact = await _contentManager.GetAsync(party.Id, VersionOptions.Published)
            ?? await _contentManager.GetAsync(party.Id, VersionOptions.Latest);

        if (contact is null || !contact.Has<OmnichannelContactPart>())
        {
            return;
        }

        var methods = _contactMethodsAccessor.Get(contact);
        var phone = methods.GetPreferredPhoneNumber() ?? (methods.PhoneNumbers.Count > 0 ? methods.PhoneNumbers[0] : null);
        var found = ContactInfo.From(contact.DisplayText, methods.GetPrimaryEmailAddress(), phone?.PhoneNumber, await GetAddressAsync(contact));
        var known = profile.Contact ?? new ContactInfo();

        profile.Contact = known with
        {
            Name = found.Name ?? known.Name,
            Email = found.Email ?? known.Email,
            Phone = found.Phone.HasValue ? found.Phone : known.Phone,
            Address = found.Address ?? known.Address,
        };
    }

    // The CRM keeps no address of its own: a contact type that needs one adds an address field. With several, the first
    // in the order the editor shows them (part position, then field position, then the stored order) that holds an
    // address is the billing address, so the choice does not depend on the order the definition happens to be stored in.
    private async Task<Address> GetAddressAsync(ContentItem contact)
    {
        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contact.ContentType);

        if (typeDefinition is null)
        {
            return null;
        }

        var typeParts = typeDefinition.Parts
            .Select((typePart, index) => (TypePart: typePart, Index: index))
            .OrderBy(entry => ParsePosition(entry.TypePart.GetSettings<ContentTypePartSettings>().Position))
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.TypePart);

        foreach (var typePart in typeParts)
        {
            var partFields = typePart.PartDefinition.Fields
                .Select((partField, index) => (PartField: partField, Index: index))
                .OrderBy(entry => ParsePosition(entry.PartField.GetSettings<ContentPartFieldSettings>().Position))
                .ThenBy(entry => entry.Index)
                .Select(entry => entry.PartField);

            foreach (var partField in partFields)
            {
                if (!string.Equals(partField.FieldDefinition.Name, AddressFieldName, StringComparison.Ordinal))
                {
                    continue;
                }

                var address = contact.Get<ContentPart>(typePart.Name)?.Get<AddressField>(partField.Name)?.Address;

                if (address is not null)
                {
                    return address.Clone();
                }
            }
        }

        return null;
    }

    // An editor position such as "2" or "2.5"; one that is missing or not a number sorts after the numbered ones.
    private static decimal ParsePosition(string position)
        => decimal.TryParse(position, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : decimal.MaxValue;
}
