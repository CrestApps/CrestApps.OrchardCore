using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// The CRM contacts a privacy request found for a person, and the person's phone numbers that found nobody because
/// several contacts share them.
/// </summary>
internal sealed class CrmPersonalDataMatch
{
    public CrmPersonalDataMatch(IReadOnlyList<ContentItem> contacts, IReadOnlyList<string> sharedPhoneNumbers)
    {
        Contacts = contacts;
        SharedPhoneNumbers = sharedPhoneNumbers;
    }

    /// <summary>
    /// Gets the latest version of each of the person's contacts.
    /// </summary>
    public IReadOnlyList<ContentItem> Contacts { get; }

    /// <summary>
    /// Gets the person's phone numbers, in E.164 form, that more than one contact has, so they found no contact.
    /// </summary>
    public IReadOnlyList<string> SharedPhoneNumbers { get; }
}
