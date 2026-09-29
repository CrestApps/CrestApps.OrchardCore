using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Picks a contact's primary contact methods: the first email address and the first phone number of each phone
/// type. This is the same first-of-its-kind rule the contact index uses for its primary email, cell and home
/// numbers, widened to every phone type so a contact whose only number is typed "Work" still shows it.
/// </summary>
internal static class ContactPrimaryMethodsResolver
{
    public static IReadOnlyList<ContactPrimaryMethod> Resolve(ContentItem contact)
    {
        if (contact is null ||
            !contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) ||
            bagPart.ContentItems is null ||
            bagPart.ContentItems.Count == 0)
        {
            return [];
        }

        var methods = new List<ContactPrimaryMethod>();
        var phoneTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasEmail = false;

        foreach (var contactMethod in bagPart.ContentItems)
        {
            if (string.Equals(contactMethod.ContentType, OmnichannelConstants.ContentTypes.EmailAddress, StringComparison.Ordinal))
            {
                if (!hasEmail &&
                    contactMethod.TryGet<EmailInfoPart>(out var emailPart) &&
                    !string.IsNullOrWhiteSpace(emailPart.Email?.Text))
                {
                    hasEmail = true;
                    methods.Add(new ContactPrimaryMethod
                    {
                        Kind = ContactPrimaryMethodKind.Email,
                        Value = emailPart.Email.Text.Trim(),
                    });
                }

                continue;
            }

            if (!string.Equals(contactMethod.ContentType, OmnichannelConstants.ContentTypes.PhoneNumber, StringComparison.Ordinal) ||
                !contactMethod.TryGet<PhoneNumberInfoPart>(out var phonePart) ||
                string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber))
            {
                continue;
            }

            var phoneType = phonePart.Type?.Text?.Trim() ?? string.Empty;

            if (!phoneTypes.Add(phoneType))
            {
                continue;
            }

            methods.Add(new ContactPrimaryMethod
            {
                Kind = ContactPrimaryMethodKind.Phone,
                Label = phoneType,
                Value = phonePart.Number.PhoneNumber.Trim(),
            });
        }

        return methods;
    }
}
