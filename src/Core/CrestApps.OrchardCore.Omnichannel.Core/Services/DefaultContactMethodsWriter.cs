using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The default <see cref="IContactMethodsWriter"/>: it hands each write to the writer of the contact's type.
/// </summary>
internal sealed class DefaultContactMethodsWriter : IContactMethodsWriter
{
    private readonly ContactMethodsOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DefaultContactMethodsWriter(
        IOptions<ContactMethodsOptions> options,
        IServiceProvider serviceProvider,
        ILogger<DefaultContactMethodsWriter> logger)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<bool> TrySetEmailAddressAsync(ContentItem contact, string emailAddress)
        => GetWriter(contact)?.TrySetEmailAddressAsync(contact, emailAddress) ?? Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> TryAddEmailAddressAsync(ContentItem contact, string emailAddress)
        => GetWriter(contact)?.TryAddEmailAddressAsync(contact, emailAddress) ?? Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> TryAddPhoneNumberAsync(ContentItem contact, ContactPhoneNumber phoneNumber)
        => GetWriter(contact)?.TryAddPhoneNumberAsync(contact, phoneNumber) ?? Task.FromResult(false);

    private IContactMethodsWriter GetWriter(ContentItem contact)
    {
        if (contact is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(contact.ContentType) &&
            _options.Writers.TryGetValue(contact.ContentType, out var writerType) &&
            writerType is not null)
        {
            return (IContactMethodsWriter)_serviceProvider.GetRequiredService(writerType);
        }

        if (_options.UsesContactMethodsBag(contact.ContentType))
        {
            return _serviceProvider.GetRequiredService<BagContactMethodsWriter>();
        }

        // The type reads its contact methods through a provider of its own and registered no writer, so nothing the
        // product learned is written over the values its own system keeps.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Contact type '{ContentType}' has a contact-methods provider and no writer, so a contact method was not written to contact '{ContentItemId}'.",
                contact.ContentType.SanitizeLogValue(),
                contact.ContentItemId.SanitizeLogValue());
        }

        return null;
    }
}
