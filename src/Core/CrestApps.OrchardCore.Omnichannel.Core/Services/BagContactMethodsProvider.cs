using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Reads a contact's phone numbers and email addresses from its <c>ContactMethods</c> bag.
/// </summary>
/// <remarks>
/// Any item in the bag that carries a <see cref="PhoneNumberInfoPart"/> is a phone number, and any item that carries
/// an <see cref="EmailInfoPart"/> is an email address, whatever its content type, because the bag accepts every
/// type of the contact-method stereotype.
/// </remarks>
internal sealed class BagContactMethodsProvider : IContactMethodsProvider
{
    /// <inheritdoc/>
    public ContactMethodSet GetContactMethods(ContentItem contact)
    {
        if (contact is null ||
            !contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) ||
            bagPart.ContentItems is not { Count: > 0 })
        {
            return ContactMethodSet.Empty;
        }

        var phoneNumbers = new List<ContactPhoneNumber>();
        var emailAddresses = new List<ContactEmailAddress>();

        foreach (var contactMethod in bagPart.ContentItems)
        {
            if (contactMethod is null)
            {
                continue;
            }

            if (contactMethod.TryGet<PhoneNumberInfoPart>(out var phonePart))
            {
                if (!string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber))
                {
                    phoneNumbers.Add(new ContactPhoneNumber
                    {
                        PhoneNumber = phonePart.Number.PhoneNumber,
                        CountryCode = phonePart.Number.CountryCode,
                        NationalNumber = phonePart.Number.NationalNumber,
                        Type = phonePart.Type?.Text,
                        Extension = phonePart.Extension?.Text,
                    });
                }

                continue;
            }

            if (contactMethod.TryGet<EmailInfoPart>(out var emailPart) &&
                !string.IsNullOrWhiteSpace(emailPart.Email?.Text))
            {
                emailAddresses.Add(new ContactEmailAddress
                {
                    Email = emailPart.Email.Text,
                });
            }
        }

        return new ContactMethodSet(phoneNumbers, emailAddresses);
    }
}
