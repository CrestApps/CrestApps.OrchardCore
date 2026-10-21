using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Automatically sets the <see cref="OmnichannelContactPart.TimeZoneId"/> from the contact's
/// phone number when no time zone was explicitly selected and the part is configured to auto
/// detect the time zone. This lets the time zone default to an automatic value derived from the
/// phone number instead of forcing the user to pick one.
/// </summary>
internal sealed class OmnichannelContactTimeZoneHandler : ContentHandlerBase
{
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContactMethodsAccessor _contactMethods;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactTimeZoneHandler"/> class.
    /// </summary>
    /// <param name="phoneNumberService">The phone number service.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="contactMethods">Reads the contact's phone numbers.</param>
    public OmnichannelContactTimeZoneHandler(
        IPhoneNumberService phoneNumberService,
        IContentDefinitionManager contentDefinitionManager,
        IContactMethodsAccessor contactMethods)
    {
        _phoneNumberService = phoneNumberService;
        _contentDefinitionManager = contentDefinitionManager;
        _contactMethods = contactMethods;
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreateContentContext context)
        => EnsureTimeZoneAsync(context.ContentItem);

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdateContentContext context)
        => EnsureTimeZoneAsync(context.ContentItem);

    private async Task EnsureTimeZoneAsync(ContentItem contentItem)
    {
        // Respect an explicit selection; only fill in the time zone when the user left it automatic.
        if (!contentItem.TryGet<OmnichannelContactPart>(out var contactPart) ||
            !string.IsNullOrEmpty(contactPart.TimeZoneId))
        {
            return;
        }

        if (!await IsAutoDetectEnabledAsync(contentItem.ContentType))
        {
            return;
        }

        var timeZoneId = ResolveTimeZoneId(contentItem);

        if (string.IsNullOrEmpty(timeZoneId))
        {
            return;
        }

        contactPart.TimeZoneId = timeZoneId;
        contentItem.Apply(contactPart);
    }

    private async Task<bool> IsAutoDetectEnabledAsync(string contentType)
    {
        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        var partDefinition = typeDefinition?.Parts
            .FirstOrDefault(part => string.Equals(part.PartDefinition.Name, nameof(OmnichannelContactPart), StringComparison.Ordinal));

        // Default to auto detect when the settings are unavailable, matching the setting's default.
        return partDefinition?.GetSettings<OmnichannelContactPartSettings>()?.AutoDetectTimeZone ?? true;
    }

    private string ResolveTimeZoneId(ContentItem contentItem)
    {
        foreach (var phoneNumber in _contactMethods.Get(contentItem).PhoneNumbers)
        {
            var timeZoneId = GetFirstKnownTimeZoneId(phoneNumber);

            if (!string.IsNullOrEmpty(timeZoneId))
            {
                return timeZoneId;
            }
        }

        return null;
    }

    private string GetFirstKnownTimeZoneId(ContactPhoneNumber field)
    {
        var rawNumber = field?.PhoneNumber?.Trim();

        if (string.IsNullOrEmpty(rawNumber))
        {
            return null;
        }

        // The time-zone lookup expects a canonical E.164 number. Canonicalize through the single parsing entry point
        // so a number that cannot be canonicalized is dropped rather than looked up as a half-normalized string.
        if (!_phoneNumberService.TryParse(rawNumber, field.CountryCode, out var phoneNumber))
        {
            return null;
        }

        foreach (var timeZoneId in _phoneNumberService.GetTimeZones(phoneNumber.Value))
        {
            var normalizedTimeZoneId = NormalizeTimeZoneId(timeZoneId);

            if (!string.IsNullOrEmpty(normalizedTimeZoneId))
            {
                return normalizedTimeZoneId;
            }
        }

        return null;
    }

    private static string NormalizeTimeZoneId(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        return NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId.Trim())?.Id;
    }
}
