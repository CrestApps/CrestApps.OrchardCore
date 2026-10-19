using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Writes phone numbers and email addresses into a contact's <c>ContactMethods</c> bag.
/// </summary>
/// <remarks>
/// Each new item is created through the content manager rather than constructed directly, so it is built the way
/// every other content item is: its type's parts and defaults are applied and it is given an identifier. An item
/// with no identifier is not merely incomplete -- OrchardCore keys a bag's items by content item id when it applies
/// an edit, so one such item stops the entire bag from saving, and the failure is silent.
/// </remarks>
internal sealed class BagContactMethodsWriter : IContactMethodsWriter
{
    private readonly IContentManager _contentManager;
    private readonly BagContactMethodsProvider _bagProvider;

    public BagContactMethodsWriter(
        IContentManager contentManager,
        BagContactMethodsProvider bagProvider)
    {
        _contentManager = contentManager;
        _bagProvider = bagProvider;
    }

    /// <inheritdoc/>
    public async Task<bool> TrySetEmailAddressAsync(ContentItem contact, string emailAddress)
    {
        var email = emailAddress?.Trim();

        if (contact is null || string.IsNullOrEmpty(email))
        {
            return false;
        }

        // Nothing to do when the same address is already the contact's address.
        var current = DefaultContactMethodsAccessor.Normalize(_bagProvider.GetContactMethods(contact)).GetPrimaryEmailAddress();

        if (string.Equals(current, email, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var bag = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        bag.ContentItems ??= [];

        // It replaces rather than appends, because a raw content-item merge added a duplicate every time.
        bag.ContentItems.RemoveAll(method => string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.EmailAddress, StringComparison.Ordinal));
        bag.ContentItems.Add(await CreateEmailAddressAsync(email));
        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> TryAddEmailAddressAsync(ContentItem contact, string emailAddress)
    {
        var email = emailAddress?.Trim();

        if (contact is null || string.IsNullOrEmpty(email))
        {
            return false;
        }

        var current = DefaultContactMethodsAccessor.Normalize(_bagProvider.GetContactMethods(contact));

        if (current.EmailAddresses.Any(existing => string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var bag = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        bag.ContentItems ??= [];
        bag.ContentItems.Add(await CreateEmailAddressAsync(email));
        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> TryAddPhoneNumberAsync(ContentItem contact, ContactPhoneNumber phoneNumber)
    {
        var number = phoneNumber?.PhoneNumber?.Trim();

        if (contact is null || string.IsNullOrEmpty(number))
        {
            return false;
        }

        // The same number in another format is the same number.
        var digits = GetDigits(number);
        var current = DefaultContactMethodsAccessor.Normalize(_bagProvider.GetContactMethods(contact));

        if (current.PhoneNumbers.Any(existing => string.Equals(GetDigits(existing.PhoneNumber), digits, StringComparison.Ordinal)))
        {
            return false;
        }

        var type = ContactPhoneTypes.Normalize(phoneNumber.Type);
        var phoneItem = await _contentManager.NewAsync(OmnichannelConstants.ContentTypes.PhoneNumber);
        phoneItem.DisplayText = string.IsNullOrEmpty(type) ? number : $"{type}: {number}";
        phoneItem.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField
            {
                PhoneNumber = number,
                CountryCode = phoneNumber.CountryCode?.Trim(),
                NationalNumber = phoneNumber.NationalNumber?.Trim(),
            };
            part.Type = new TextField { Text = type };
            part.Extension = string.IsNullOrWhiteSpace(phoneNumber.Extension)
                ? null
                : new TextField { Text = phoneNumber.Extension.Trim() };
        });

        var bag = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        bag.ContentItems ??= [];
        bag.ContentItems.Add(phoneItem);
        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return true;
    }

    private async Task<ContentItem> CreateEmailAddressAsync(string email)
    {
        var emailItem = await _contentManager.NewAsync(OmnichannelConstants.ContentTypes.EmailAddress);
        emailItem.DisplayText = email;
        emailItem.Alter<EmailInfoPart>(part => part.Email = new TextField { Text = email });

        return emailItem;
    }

    private static string GetDigits(string value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());
}
