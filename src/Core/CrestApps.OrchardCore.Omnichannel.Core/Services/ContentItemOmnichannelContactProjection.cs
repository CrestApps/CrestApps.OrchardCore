using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Reads an Orchard Core contact content item as an <see cref="OmnichannelContact"/>.
/// </summary>
/// <remarks>
/// One place, because a contact is read from several services and a projection that disagrees with
/// itself would show one opt-out state on screen and act on another.
/// </remarks>
public static class ContentItemOmnichannelContactProjection
{
    /// <summary>
    /// Projects a contact content item.
    /// </summary>
    /// <param name="contentItem">The content item, or <see langword="null"/>.</param>
    /// <returns>The contact, or <see langword="null"/> when there is no content item.</returns>
    public static OmnichannelContact Project(ContentItem contentItem)
    {
        if (contentItem is null)
        {
            return null;
        }

        var contact = new OmnichannelContact
        {
            Id = contentItem.ContentItemId,
            DefinitionName = contentItem.ContentType,
            DisplayText = contentItem.DisplayText,
        };

        // TryGet rather than As, because reading a contact must not create an empty part on an item
        // that has never carried one; this is only a question.
        if (contentItem.TryGet<OmnichannelContactPart>(out var contactPart))
        {
            contact.TimeZoneId = contactPart.TimeZoneId;
            contact.DoNotCall = contactPart.DoNotCall;
            contact.DoNotCallUtc = contactPart.DoNotCallUtc;
            contact.DoNotSms = contactPart.DoNotSms;
            contact.DoNotSmsUtc = contactPart.DoNotSmsUtc;
            contact.DoNotEmail = contactPart.DoNotEmail;
            contact.DoNotEmailUtc = contactPart.DoNotEmailUtc;
        }

        if (contentItem.TryGet<OmnichannelContactInfoPart>(out var infoPart))
        {
            contact.FirstName = infoPart.FirstName?.Text;
            contact.LastName = infoPart.LastName?.Text;
        }

        ProjectContactMethods(contentItem, contact);

        return contact;
    }

    /// <summary>
    /// Reads the contact-methods bag onto the projection.
    /// </summary>
    /// <param name="contentItem">The contact content item.</param>
    /// <param name="contact">The projection being filled.</param>
    private static void ProjectContactMethods(ContentItem contentItem, OmnichannelContact contact)
    {
        if (!contentItem.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bag) ||
            bag.ContentItems is null)
        {
            return;
        }

        foreach (var method in bag.ContentItems)
        {
            if (string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.EmailAddress, StringComparison.Ordinal))
            {
                if (method.TryGet<EmailInfoPart>(out var emailPart) && !string.IsNullOrWhiteSpace(emailPart.Email?.Text))
                {
                    contact.Emails.Add(new ContactEmail { Email = emailPart.Email.Text.Trim() });
                }

                continue;
            }

            if (string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.PhoneNumber, StringComparison.Ordinal) &&
                method.TryGet<PhoneNumberInfoPart>(out var phonePart) &&
                !string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber))
            {
                contact.PhoneNumbers.Add(new ContactPhoneNumber
                {
                    Number = phonePart.Number.PhoneNumber.Trim(),
                    Extension = phonePart.Extension?.Text,
                    Type = phonePart.Type?.Text,
                });
            }
        }
    }
}
