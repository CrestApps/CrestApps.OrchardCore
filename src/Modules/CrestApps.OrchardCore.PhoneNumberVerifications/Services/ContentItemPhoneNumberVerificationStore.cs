using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumberVerifications.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Stores verification results on the <see cref="PhoneNumberVerificationPart"/> of a content item.
/// </summary>
public sealed class ContentItemPhoneNumberVerificationStore : IPhoneNumberVerificationStore
{
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemPhoneNumberVerificationStore"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read module settings.</param>
    /// <param name="clock">The clock used for time calculations.</param>
    public ContentItemPhoneNumberVerificationStore(
        ISiteService siteService,
        IClock clock)
    {
        _siteService = siteService;
        _clock = clock;
    }

    /// <inheritdoc/>
    public PhoneNumberVerificationResult Read(ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        if (!contentItem.TryGet<PhoneNumberVerificationPart>(out var part) || string.IsNullOrEmpty(part.VerificationResultJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PhoneNumberVerificationResult>(part.VerificationResultJson, PhoneNumberVerificationSerialization.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        ContentItem contentItem,
        PhoneNumberVerificationResult result,
        string verifiedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentItem);
        ArgumentNullException.ThrowIfNull(result);

        var settings = await _siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();
        var part = contentItem.GetOrCreate<PhoneNumberVerificationPart>();

        part.VerificationStatus = result.Status;
        part.VerificationProvider = result.VerificationProvider;
        part.VerificationResultJson = JsonSerializer.Serialize(result, PhoneNumberVerificationSerialization.Options);
        part.VerificationAttemptCount += 1;
        part.LastVerifiedByUserId = verifiedByUserId;

        if (result.Status == PhoneNumberVerificationStatus.Verified || result.Status == PhoneNumberVerificationStatus.Invalid)
        {
            part.LastVerifiedUtc = result.VerificationDateUtc;
            part.NextVerificationDueUtc = result.VerificationDateUtc.AddDays(GetRevalidationIntervalDays(settings));
        }

        contentItem.Apply(part);
    }

    /// <inheritdoc/>
    public async Task<bool> RequiresRevalidationAsync(ContentItem contentItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        if (!contentItem.TryGet<PhoneNumberVerificationPart>(out var part) || part.LastVerifiedUtc is null)
        {
            return true;
        }

        if (part.NextVerificationDueUtc is null)
        {
            var settings = await _siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();

            return part.LastVerifiedUtc.Value.AddDays(GetRevalidationIntervalDays(settings)) <= _clock.UtcNow;
        }

        return part.NextVerificationDueUtc.Value <= _clock.UtcNow;
    }

    /// <inheritdoc/>
    public bool IsVerified(ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        return contentItem.TryGet<PhoneNumberVerificationPart>(out var part)
            && part.VerificationStatus == PhoneNumberVerificationStatus.Verified
            && part.LastVerifiedUtc is not null;
    }

    private static int GetRevalidationIntervalDays(PhoneNumberVerificationsSettings settings)
    {
        return settings.RevalidationIntervalDays > 0
            ? settings.RevalidationIntervalDays
            : PhoneNumberVerificationsSettings.DefaultRevalidationIntervalDays;
    }
}
