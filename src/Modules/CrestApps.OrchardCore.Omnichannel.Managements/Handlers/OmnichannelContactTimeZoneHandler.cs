using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Flows.Models;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactTimeZoneHandler"/> class.
    /// </summary>
    /// <param name="phoneNumberService">The phone number service.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public OmnichannelContactTimeZoneHandler(
        IPhoneNumberService phoneNumberService,
        IContentDefinitionManager contentDefinitionManager)
    {
        _phoneNumberService = phoneNumberService;
        _contentDefinitionManager = contentDefinitionManager;
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
        if (!contentItem.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) ||
            bagPart.ContentItems is null)
        {
            return null;
        }

        foreach (var contactMethod in bagPart.ContentItems)
        {
            if (!string.Equals(contactMethod.ContentType, OmnichannelConstants.ContentTypes.PhoneNumber, StringComparison.Ordinal) ||
                !contactMethod.TryGet<PhoneNumberInfoPart>(out var phonePart))
            {
                continue;
            }

            var timeZoneId = GetFirstKnownTimeZoneId(phonePart.Number);

            if (!string.IsNullOrEmpty(timeZoneId))
            {
                return timeZoneId;
            }
        }

        return null;
    }

    private string GetFirstKnownTimeZoneId(PhoneField field)
    {
        var e164Number = field?.PhoneNumber?.Trim();

        if (string.IsNullOrEmpty(e164Number))
        {
            return null;
        }

        // The time-zone lookup expects an E.164 number; normalize when the stored value is not already E.164.
        if (!PhoneNumber.IsE164(e164Number) &&
            _phoneNumberService.TryFormatToE164(e164Number, field.CountryCode, out var formattedNumber))
        {
            e164Number = formattedNumber;
        }

        foreach (var timeZoneId in _phoneNumberService.GetTimeZones(e164Number))
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
