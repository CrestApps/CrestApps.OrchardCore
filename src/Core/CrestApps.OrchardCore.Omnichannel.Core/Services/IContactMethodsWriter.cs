using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Writes phone numbers and email addresses the product learned onto a contact: an email address given in a
/// conversation, or the numbers and addresses of a lead that is converted into the contact.
/// </summary>
/// <remarks>
/// Inject it to write to any contact: it writes to the <c>ContactMethods</c> bag, or hands the write to the writer
/// registered for the contact's type with <c>AddContactMethodsWriter</c>. A type with a provider of its own and no
/// writer is read-only, and every write to it returns <see langword="false"/>.
/// <para>
/// A writer registered for a type decides how a write lands, for example that it never overwrites a value the
/// type's own system owns. It only changes the content item; the caller saves it.
/// </para>
/// </remarks>
public interface IContactMethodsWriter
{
    /// <summary>
    /// Makes an email address the contact's email address, replacing the one it had.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <param name="emailAddress">The email address.</param>
    /// <returns>Whether the contact changed.</returns>
    Task<bool> TrySetEmailAddressAsync(ContentItem contact, string emailAddress);

    /// <summary>
    /// Adds an email address to the contact, unless it already has it.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <param name="emailAddress">The email address.</param>
    /// <returns>Whether the contact changed.</returns>
    Task<bool> TryAddEmailAddressAsync(ContentItem contact, string emailAddress);

    /// <summary>
    /// Adds a phone number to the contact, unless it already has the same number.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <param name="phoneNumber">The phone number.</param>
    /// <returns>Whether the contact changed.</returns>
    Task<bool> TryAddPhoneNumberAsync(ContentItem contact, ContactPhoneNumber phoneNumber);
}
