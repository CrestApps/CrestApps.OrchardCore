using System.Collections.Concurrent;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The default <see cref="IContactMethodsAccessor"/>: it reads a contact through the provider registered for its
/// content type, and through the <c>ContactMethods</c> bag when there is none.
/// </summary>
internal sealed class DefaultContactMethodsAccessor : IContactMethodsAccessor
{
    private readonly ContactMethodsOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly BagContactMethodsProvider _bagProvider;
    private readonly ConcurrentDictionary<Type, IContactMethodsProvider> _providers = new();

    public DefaultContactMethodsAccessor(
        IOptions<ContactMethodsOptions> options,
        IServiceProvider serviceProvider,
        BagContactMethodsProvider bagProvider)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _bagProvider = bagProvider;
    }

    /// <inheritdoc/>
    public ContactMethodSet Get(ContentItem contact)
    {
        if (contact is null)
        {
            return ContactMethodSet.Empty;
        }

        return Normalize(GetProvider(contact.ContentType).GetContactMethods(contact));
    }

    /// <inheritdoc/>
    public bool UsesContactMethodsBag(string contentType)
        => _options.UsesContactMethodsBag(contentType);

    private IContactMethodsProvider GetProvider(string contentType)
    {
        if (string.IsNullOrEmpty(contentType) ||
            !_options.Providers.TryGetValue(contentType, out var providerType) ||
            providerType is null)
        {
            return _bagProvider;
        }

        return _providers.GetOrAdd(
            providerType,
            static (type, serviceProvider) => (IContactMethodsProvider)serviceProvider.GetRequiredService(type),
            _serviceProvider);
    }

    /// <summary>
    /// Leaves out the numbers and addresses with no value, trims the values, and spells each phone type the one way
    /// every reader matches it.
    /// </summary>
    internal static ContactMethodSet Normalize(ContactMethodSet contactMethods)
    {
        if (contactMethods is null)
        {
            return ContactMethodSet.Empty;
        }

        var phoneNumbers = new List<ContactPhoneNumber>(contactMethods.PhoneNumbers.Count);

        foreach (var phoneNumber in contactMethods.PhoneNumbers)
        {
            var number = phoneNumber?.PhoneNumber?.Trim();

            if (string.IsNullOrEmpty(number))
            {
                continue;
            }

            phoneNumbers.Add(new ContactPhoneNumber
            {
                PhoneNumber = number,
                CountryCode = TrimToNull(phoneNumber.CountryCode),
                NationalNumber = TrimToNull(phoneNumber.NationalNumber),
                Type = ContactPhoneTypes.Normalize(phoneNumber.Type),
                Extension = TrimToNull(phoneNumber.Extension),
            });
        }

        var emailAddresses = new List<ContactEmailAddress>(contactMethods.EmailAddresses.Count);

        foreach (var emailAddress in contactMethods.EmailAddresses)
        {
            var email = emailAddress?.Email?.Trim();

            if (string.IsNullOrEmpty(email))
            {
                continue;
            }

            emailAddresses.Add(new ContactEmailAddress
            {
                Email = email,
            });
        }

        if (phoneNumbers.Count == 0 && emailAddresses.Count == 0)
        {
            return ContactMethodSet.Empty;
        }

        return new ContactMethodSet(phoneNumbers, emailAddresses);
    }

    private static string TrimToNull(string value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed;
    }
}
