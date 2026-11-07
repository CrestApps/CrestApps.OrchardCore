using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Exports and anonymizes the CRM contacts and leads of a person, found by their email or a phone number no other contact
/// has. It adds nothing to the person it is asked about, because an inbox or a number on a contact may be shared.
/// Erasing keeps the contact, so its activities and the reports built on them still add up, and clears only its known
/// personal-data fields (see <see cref="CrmContactPersonalDataFields"/>): the display text and title, the name fields,
/// the emails and phone numbers, any field holding one of those, and the AI memory note about the contact. Its
/// references to other content items and users are never touched.
/// </summary>
internal sealed class CrmContactPersonalDataContributor : IPersonalDataContributor
{
    public const string ContactKind = "CrmContact";

    public const string LeadKind = "CrmLead";

    private readonly CrmPersonalDataContacts _contacts;
    private readonly IContactMethodsAccessor _contactMethods;
    private readonly IContentManager _contentManager;
    private readonly IAIContactMemoryStore _memories;

    internal readonly IStringLocalizer S;

    public CrmContactPersonalDataContributor(
        CrmPersonalDataContacts contacts,
        IContactMethodsAccessor contactMethods,
        IContentManager contentManager,
        IStringLocalizer<CrmContactPersonalDataContributor> stringLocalizer,
        IEnumerable<IAIContactMemoryStore> memoryStores)
    {
        _contacts = contacts;
        _contactMethods = contactMethods;
        _contentManager = contentManager;
        S = stringLocalizer;

        // The AI memory store is registered by the AI feature, which may be off.
        _memories = memoryStores?.FirstOrDefault();
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var match = await _contacts.MatchAsync(context.Subject, cancellationToken);

        foreach (var phoneNumber in match.SharedPhoneNumbers)
        {
            context.Export.Warnings.Add(SharedPhoneNumberWarning(phoneNumber));
        }

        foreach (var contact in match.Contacts)
        {
            var isLead = contact.TryGet<LeadPart>(out var lead);
            var methods = _contactMethods.Get(contact);
            var record = context.Add(isLead ? LeadKind : ContactKind, contact.ContentItemId, contact.DisplayText, contact.CreatedUtc);

            record.Fields[nameof(ContentItem.ContentType)] = contact.ContentType;
            record.Fields[nameof(ContentItem.DisplayText)] = contact.DisplayText;
            record.Fields[nameof(ContactMethodSet.EmailAddresses)] = string.Join(", ", methods.EmailAddresses.Select(email => email?.Email).Where(email => !string.IsNullOrEmpty(email)));
            record.Fields[nameof(ContactMethodSet.PhoneNumbers)] = string.Join(", ", methods.PhoneNumbers.Select(phone => phone?.PhoneNumber).Where(phone => !string.IsNullOrEmpty(phone)));

            if (isLead)
            {
                record.Fields[nameof(LeadPart.StatusId)] = lead.StatusId;
                record.Fields[nameof(LeadPart.IsConverted)] = lead.IsConverted.ToString(CultureInfo.InvariantCulture);
            }

            // Every field the contact type holds, as stored, so nothing the person gave is left out of their copy.
            record.Fields["Content"] = ((JsonObject)contact.Content).ToJsonString();

            if (_memories is not null)
            {
                record.Fields["AIMemory"] = (await _memories.FindByContactAsync(contact.ContentItemId, cancellationToken))?.Text;
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var match = await _contacts.MatchAsync(context.Subject, cancellationToken);

        foreach (var phoneNumber in match.SharedPhoneNumbers)
        {
            context.Result.Warnings.Add(SharedPhoneNumberWarning(phoneNumber));
        }

        foreach (var contact in match.Contacts)
        {
            // Only the known personal-data fields are cleared: scrubbing every string of the content would also change
            // the ids of the company, owner and status the contact points at.
            CrmContactPersonalDataFields.Clear(contact, _contactMethods.Get(contact), context.ReplacementText ?? S["Anonymized contact"].Value);

            await _contentManager.UpdateAsync(contact);

            if (_memories is not null && await _memories.FindByContactAsync(contact.ContentItemId, cancellationToken) is { } memory)
            {
                await _memories.DeleteAsync(memory, cancellationToken);
            }

            context.Result.Count(contact.Has<LeadPart>() ? LeadKind : ContactKind);
        }
    }

    private string SharedPhoneNumberWarning(string phoneNumber)
        => S["The phone number {0} belongs to more than one CRM contact, so no contact was found by it. Review those contacts by hand.", phoneNumber].Value;
}
