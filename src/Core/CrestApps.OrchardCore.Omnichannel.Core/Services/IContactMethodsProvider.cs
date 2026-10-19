using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Supplies the phone numbers and email addresses of the contacts of one or more content types.
/// </summary>
/// <remarks>
/// A contact type keeps its contact methods in the <c>ContactMethods</c> bag unless it registers a provider of its
/// own, for example to read the typed fields of a person record it already has.
/// <para>
/// A provider is a singleton, and it must read only what is stored on the content item it is given: no database
/// queries and no scoped services. The contact index is built from what it returns while the item is being saved,
/// and is not rebuilt when anything else changes, so a value read from another record would leave the index stale.
/// </para>
/// </remarks>
public interface IContactMethodsProvider
{
    /// <summary>
    /// Reads the contact's phone numbers and email addresses, in the order the contact keeps them. Return each phone
    /// number as it was entered, with the region it was entered for; it is normalized where it is used.
    /// </summary>
    /// <param name="contact">The contact.</param>
    ContactMethodSet GetContactMethods(ContentItem contact);
}
