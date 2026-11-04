using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Exports and anonymizes the CRM contacts and leads of a person, found by their email or phone number. Erasing keeps
/// the contact, so its activities and the reports built on them still add up, but replaces its name and removes the
/// person's emails, phone numbers and name from every field.
/// </summary>
internal sealed class CrmContactPersonalDataContributor : IPersonalDataContributor
{
    public const string ContactKind = "CrmContact";

    public const string LeadKind = "CrmLead";

    private readonly CrmPersonalDataContacts _contacts;
    private readonly IContactMethodsAccessor _contactMethods;
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    public CrmContactPersonalDataContributor(
        CrmPersonalDataContacts contacts,
        IContactMethodsAccessor contactMethods,
        IContentManager contentManager,
        IStringLocalizer<CrmContactPersonalDataContributor> stringLocalizer)
    {
        _contacts = contacts;
        _contactMethods = contactMethods;
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    public async Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var contact in await _contacts.FindAsync(subject, cancellationToken))
        {
            var methods = _contactMethods.Get(contact);

            foreach (var email in methods.EmailAddresses)
            {
                subject.AddEmail(email?.Email);
            }

            foreach (var phoneNumber in methods.PhoneNumbers)
            {
                subject.AddPhoneNumber(phoneNumber?.PhoneNumber);
            }

            subject.AddName(contact.DisplayText);
        }
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var contact in await _contacts.FindAsync(context.Subject, cancellationToken))
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
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var contact in await _contacts.FindAsync(context.Subject, cancellationToken))
        {
            context.Subject.ScrubJson((JsonObject)contact.Content, context.ReplacementText);
            contact.DisplayText = context.ReplacementText ?? S["Anonymized contact"].Value;

            await _contentManager.UpdateAsync(contact);
            context.Result.Count(contact.Has<LeadPart>() ? LeadKind : ContactKind);
        }
    }
}
