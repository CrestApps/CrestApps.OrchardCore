using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Reads a contact's phone numbers and email addresses, from wherever its content type keeps them.
/// </summary>
/// <remarks>
/// Every part of the product that needs a contact's numbers or addresses reads them here: the contact index, the
/// activity destinations, the contact header, the messaging workspace, the time zone detection and the phone
/// verification. A contact type keeps them in its <c>ContactMethods</c> bag unless it registers an
/// <see cref="IContactMethodsProvider"/> of its own.
/// <para>
/// It is a singleton and synchronous, because the values are stored on the contact item itself, so the contact index
/// can read them while the item is being indexed.
/// </para>
/// </remarks>
public interface IContactMethodsAccessor
{
    /// <summary>
    /// Reads the contact's phone numbers and email addresses. Numbers and addresses with no value are left out, the
    /// values are trimmed, and each phone type is spelled as <see cref="ContactPhoneTypes.Normalize"/> spells it.
    /// </summary>
    /// <param name="contact">The contact, or <see langword="null"/>.</param>
    /// <returns>The contact's contact methods, or <see cref="ContactMethodSet.Empty"/> when there are none.</returns>
    ContactMethodSet Get(ContentItem contact);

    /// <summary>
    /// Whether contacts of a content type keep their phone numbers and email addresses in the
    /// <c>ContactMethods</c> bag, which is so for every type without a provider of its own.
    /// </summary>
    /// <param name="contentType">The content type name.</param>
    bool UsesContactMethodsBag(string contentType);
}
