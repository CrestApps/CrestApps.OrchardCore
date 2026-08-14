using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Resolves the preferred phone number for an omnichannel contact from its contact-methods bag.
/// Centralizes the selection and priority logic shared by the verification handler and the
/// background revalidation task so both stay in sync.
/// </summary>
internal static class OmnichannelContactPhoneNumberResolver
{
    /// <summary>
    /// Selects the highest-priority phone-number content item from a contact's contact-methods bag.
    /// </summary>
    /// <param name="contact">The omnichannel contact content item.</param>
    /// <returns>The preferred phone-number content item, or <see langword="null"/> when none is available.</returns>
    public static ContentItem GetPreferredPhoneNumberContentItem(ContentItem contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        if (!contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart)
            || bagPart.ContentItems is null
            || bagPart.ContentItems.Count == 0)
        {
            return null;
        }

        var phoneNumbers = new PriorityQueue<ContentItem, int>();

        foreach (var contentMethod in bagPart.ContentItems)
        {
            if (contentMethod.ContentType != OmnichannelConstants.ContentTypes.PhoneNumber
                || !contentMethod.TryGet<PhoneNumberInfoPart>(out var phonePart)
                || string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber))
            {
                continue;
            }

            var priority = GetPhoneNumberPriority(phonePart.Type?.Text);

            if (priority is null)
            {
                continue;
            }

            phoneNumbers.Enqueue(contentMethod, priority.Value);
        }

        return phoneNumbers.Count > 0
            ? phoneNumbers.Dequeue()
            : null;
    }

    /// <summary>
    /// Reads the trimmed phone number stored on a phone-number content item.
    /// </summary>
    /// <param name="phoneNumberContentItem">The phone-number content item, or <see langword="null"/>.</param>
    /// <returns>The trimmed phone number, or <see langword="null"/> when none is available.</returns>
    public static string GetPhoneNumber(ContentItem phoneNumberContentItem)
    {
        return phoneNumberContentItem is not null
            && phoneNumberContentItem.TryGet<PhoneNumberInfoPart>(out var phonePart)
            && !string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber)
            ? phonePart.Number.PhoneNumber.Trim()
            : null;
    }

    private static int? GetPhoneNumberPriority(string type)
    {
        return type switch
        {
            "Cell" => 1,
            "Home" => 2,
            "Office" => 3,
            "Work" => 4,
            "Other" => 5,
            _ => null,
        };
    }
}
