using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;
using OrchardCore;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// Builds CRM contacts with names, emails and phone numbers for the personal-data tests.
/// </summary>
internal static class CrmPersonalDataTestContacts
{
    /// <summary>
    /// Builds a published contact with the given name, emails and phone numbers in its <c>ContactMethods</c> bag, and
    /// its first and last name in the type's own part, the way the CRM starter recipe defines a contact.
    /// </summary>
    /// <param name="displayText">The contact's name.</param>
    /// <param name="emails">The contact's emails.</param>
    /// <param name="phoneNumbers">The contact's phone numbers.</param>
    public static ContentItem Create(string displayText, string[] emails, string[] phoneNumbers)
    {
        var contact = ContactPhoneTestStore.CreateRecord(
            ContactPhoneTestStore.ContactContentType,
            [.. phoneNumbers.Select(number => ("Cell", number))]);

        contact.DisplayText = displayText;

        var names = displayText.Split(' ', 2);
        contact.Content[ContactPhoneTestStore.ContactContentType] = new JsonObject
        {
            ["FirstName"] = new JsonObject { ["Text"] = names[0] },
            ["LastName"] = new JsonObject { ["Text"] = names.Length > 1 ? names[1] : null },
        };

        var bag = contact.Get<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);

        foreach (var email in emails)
        {
            var method = new ContentItem
            {
                ContentItemId = IdGenerator.GenerateId(),
                ContentType = OmnichannelConstants.ContentTypes.EmailAddress,
            };

            method.Alter<EmailInfoPart>(part => part.Email = new TextField { Text = email });
            bag.ContentItems.Add(method);
        }

        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return contact;
    }
}
