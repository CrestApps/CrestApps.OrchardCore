using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Picks a contact's primary contact methods: the first phone number of each phone type, then the first email
/// address. This is the same first-of-its-kind rule the contact index uses for its primary email, cell and home
/// numbers, widened to every phone type so a contact whose only number is typed "Work" still shows it. Only a cell
/// (mobile) number is marked as able to receive text messages.
/// </summary>
internal static class ContactPrimaryMethodsResolver
{
    public static IReadOnlyList<ContactPrimaryMethod> Resolve(ContactMethodSet contactMethods)
    {
        if (contactMethods is null)
        {
            return [];
        }

        var methods = new List<ContactPrimaryMethod>();
        var phoneTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var phoneNumber in contactMethods.PhoneNumbers)
        {
            var phoneType = phoneNumber.Type ?? string.Empty;

            if (!phoneTypes.Add(phoneType))
            {
                continue;
            }

            methods.Add(new ContactPrimaryMethod
            {
                Kind = ContactPrimaryMethodKind.Phone,
                Label = phoneType,
                Value = phoneNumber.PhoneNumber,
                CanText = IsMobileType(phoneType),
            });
        }

        if (contactMethods.GetPrimaryEmailAddress() is { } email)
        {
            methods.Add(new ContactPrimaryMethod
            {
                Kind = ContactPrimaryMethodKind.Email,
                Value = email,
            });
        }

        return methods;
    }

    /// <summary>
    /// Determines whether a phone number of the given type can receive text messages. Only a mobile number can; a
    /// home, work or fax line cannot.
    /// </summary>
    public static bool IsMobileType(string phoneType)
        => ContactPhoneTypes.IsMobile(phoneType);
}
