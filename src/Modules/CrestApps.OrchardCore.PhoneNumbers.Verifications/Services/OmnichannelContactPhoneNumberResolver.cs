using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Resolves the phone number an omnichannel contact's verification is about, and the contact-methods bag item that
/// holds it. Centralizes the selection shared by the verification handler and the queue processor so both stay in
/// sync.
/// </summary>
/// <remarks>
/// The verification itself is stored on the contact. A contact whose type keeps its numbers in the
/// <c>ContactMethods</c> bag also carries a copy on the bag item that holds the number, which is what shows the
/// verification next to the number in the bag's editor. A type that keeps its numbers in fields of its own has no
/// such item, and its bag, if it still has one, is left as it is.
/// </remarks>
internal static class OmnichannelContactPhoneNumberResolver
{
    /// <summary>
    /// The number the contact's verification is about: the number it is called on first.
    /// </summary>
    /// <param name="contactMethods">Reads the contact's phone numbers.</param>
    /// <param name="contact">The omnichannel contact content item.</param>
    /// <returns>The preferred phone number, or <see langword="null"/> when none is available.</returns>
    public static ContactPhoneNumber GetPreferredPhoneNumber(IContactMethodsAccessor contactMethods, ContentItem contact)
    {
        ArgumentNullException.ThrowIfNull(contactMethods);
        ArgumentNullException.ThrowIfNull(contact);

        return contactMethods.Get(contact).GetPreferredPhoneNumber();
    }

    /// <summary>
    /// Changes the contact-methods bag item that holds a phone number, and stores the bag back on the contact when the
    /// item changed, so the change is kept.
    /// </summary>
    /// <param name="contactMethods">Tells whether the contact keeps its numbers in the bag.</param>
    /// <param name="contact">The omnichannel contact content item.</param>
    /// <param name="phoneNumber">The phone number, as the accessor read it.</param>
    /// <param name="alter">Changes the item, and returns whether it changed anything.</param>
    /// <returns>Whether an item was found and changed.</returns>
    public static bool AlterPhoneNumberItem(
        IContactMethodsAccessor contactMethods,
        ContentItem contact,
        ContactPhoneNumber phoneNumber,
        Func<ContentItem, bool> alter)
    {
        ArgumentNullException.ThrowIfNull(contactMethods);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(alter);

        if (string.IsNullOrEmpty(phoneNumber?.PhoneNumber) ||
            !contactMethods.UsesContactMethodsBag(contact.ContentType) ||
            !contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) ||
            bagPart.ContentItems is not { Count: > 0 })
        {
            return false;
        }

        foreach (var contactMethod in bagPart.ContentItems)
        {
            if (contactMethod is null ||
                !contactMethod.TryGet<PhoneNumberInfoPart>(out var phonePart) ||
                !string.Equals(phonePart.Number?.PhoneNumber?.Trim(), phoneNumber.PhoneNumber, StringComparison.Ordinal) ||
                !string.Equals(ContactPhoneTypes.Normalize(phonePart.Type?.Text), phoneNumber.Type, StringComparison.Ordinal))
            {
                continue;
            }

            if (!alter(contactMethod))
            {
                return false;
            }

            contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bagPart);

            return true;
        }

        return false;
    }
}
