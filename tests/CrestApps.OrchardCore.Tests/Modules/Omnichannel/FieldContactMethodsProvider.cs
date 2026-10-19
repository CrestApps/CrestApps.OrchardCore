using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// A person record that keeps its phone numbers and email address in typed fields of its own, the way a CRM's
/// person type does, instead of in the <c>ContactMethods</c> bag.
/// </summary>
public sealed class PersonContactPart : ContentPart
{
    public PhoneField Mobile { get; set; }

    public PhoneField Landline { get; set; }

    public TextField Email { get; set; }
}

/// <summary>
/// Reads a <see cref="PersonContactPart"/>'s fields as the contact's contact methods.
/// </summary>
internal sealed class FieldContactMethodsProvider : IContactMethodsProvider
{
    public ContactMethodSet GetContactMethods(ContentItem contact)
    {
        if (!contact.TryGet<PersonContactPart>(out var person))
        {
            return ContactMethodSet.Empty;
        }

        var phoneNumbers = new List<ContactPhoneNumber>();

        Add(phoneNumbers, person.Mobile, "mobile");
        Add(phoneNumbers, person.Landline, "home");

        var emailAddresses = string.IsNullOrWhiteSpace(person.Email?.Text)
            ? []
            : new List<ContactEmailAddress> { new() { Email = person.Email.Text } };

        return new ContactMethodSet(phoneNumbers, emailAddresses);
    }

    private static void Add(List<ContactPhoneNumber> phoneNumbers, PhoneField field, string type)
    {
        if (string.IsNullOrWhiteSpace(field?.PhoneNumber))
        {
            return;
        }

        phoneNumbers.Add(new ContactPhoneNumber
        {
            PhoneNumber = field.PhoneNumber,
            CountryCode = field.CountryCode,
            NationalNumber = field.NationalNumber,
            Type = type,
        });
    }
}
