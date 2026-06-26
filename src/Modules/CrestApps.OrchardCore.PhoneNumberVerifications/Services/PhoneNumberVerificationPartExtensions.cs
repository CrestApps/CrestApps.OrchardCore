using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumberVerifications.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Provides helpers for reading and updating phone number verification data stored on content items.
/// </summary>
public static class PhoneNumberVerificationPartExtensions
{
    /// <summary>
    /// Attempts to read the stored verification result from a content item.
    /// </summary>
    /// <param name="contentItem">The content item that may carry a verification part.</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the stored result.</param>
    /// <returns><see langword="true"/> when a stored result exists and can be read; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetPhoneNumberVerificationResult(
        this ContentItem contentItem,
        out PhoneNumberVerificationResult result)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        result = null;

        return contentItem.TryGet<PhoneNumberVerificationPart>(out var part)
            && part.TryGetPhoneNumberVerificationResult(out result);
    }

    /// <summary>
    /// Attempts to read the stored verification result from a verification part.
    /// </summary>
    /// <param name="part">The verification part.</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the stored result.</param>
    /// <returns><see langword="true"/> when a stored result exists and can be read; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetPhoneNumberVerificationResult(
        this PhoneNumberVerificationPart part,
        out PhoneNumberVerificationResult result)
    {
        ArgumentNullException.ThrowIfNull(part);

        result = null;

        if (string.IsNullOrEmpty(part.VerificationResultJson))
        {
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize<PhoneNumberVerificationResult>(part.VerificationResultJson, PhoneNumberVerificationSerialization.Options);
        }
        catch (JsonException)
        {
            return false;
        }

        return result is not null;
    }

    /// <summary>
    /// Stores a verification result on a content item by altering its <see cref="PhoneNumberVerificationPart"/>.
    /// </summary>
    /// <param name="contentItem">The content item to update.</param>
    /// <param name="result">The verification result to store.</param>
    /// <param name="verifiedByUserId">The identifier of the user who triggered the verification, if any.</param>
    /// <param name="revalidationIntervalDays">The number of days before the verification should be refreshed.</param>
    public static void AlterPhoneNumberVerificationResult(
        this ContentItem contentItem,
        PhoneNumberVerificationResult result,
        string verifiedByUserId = null,
        int revalidationIntervalDays = PhoneNumberVerificationsSettings.DefaultRevalidationIntervalDays)
    {
        ArgumentNullException.ThrowIfNull(contentItem);
        ArgumentNullException.ThrowIfNull(result);

        var normalizedIntervalDays = NormalizeRevalidationIntervalDays(revalidationIntervalDays);
        var lastVerifiedUtc = GetLastVerifiedUtc(result);
        var nextVerificationDueUtc = lastVerifiedUtc?.AddDays(normalizedIntervalDays);
        var serializedResult = JsonSerializer.Serialize(result, PhoneNumberVerificationSerialization.Options);

        contentItem.Alter<PhoneNumberVerificationPart>(part =>
        {
            part.PhoneNumber = result.PhoneNumber;
            part.NormalizedPhoneNumber = result.NormalizedPhoneNumber ?? result.PhoneNumber;
            part.VerificationStatus = result.Status;
            part.VerificationProvider = result.VerificationProvider;
            part.VerificationResultJson = serializedResult;
            part.VerificationAttemptCount++;
            part.LastVerifiedByUserId = verifiedByUserId;
            part.LastVerifiedUtc = lastVerifiedUtc;
            part.NextVerificationDueUtc = nextVerificationDueUtc;
        });
    }

    /// <summary>
    /// Determines whether a content item currently has a successful verification result.
    /// </summary>
    /// <param name="contentItem">The content item to evaluate.</param>
    /// <returns><see langword="true"/> when the content item is verified; otherwise, <see langword="false"/>.</returns>
    public static bool IsPhoneNumberVerified(this ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        return contentItem.TryGet<PhoneNumberVerificationPart>(out var part)
            && part.VerificationStatus == PhoneNumberVerificationStatus.Verified
            && part.LastVerifiedUtc is not null;
    }

    /// <summary>
    /// Determines whether the stored verification result is missing or due for revalidation.
    /// </summary>
    /// <param name="contentItem">The content item to evaluate.</param>
    /// <param name="utcNow">The current UTC timestamp.</param>
    /// <param name="revalidationIntervalDays">The number of days before the verification should be refreshed.</param>
    /// <returns><see langword="true"/> when verification should be refreshed; otherwise, <see langword="false"/>.</returns>
    public static bool RequiresPhoneNumberRevalidation(
        this ContentItem contentItem,
        DateTime utcNow,
        int revalidationIntervalDays = PhoneNumberVerificationsSettings.DefaultRevalidationIntervalDays)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        if (!contentItem.TryGet<PhoneNumberVerificationPart>(out var part) || part.LastVerifiedUtc is null)
        {
            return true;
        }

        if (part.NextVerificationDueUtc is not null)
        {
            return part.NextVerificationDueUtc.Value <= utcNow;
        }

        return part.LastVerifiedUtc.Value.AddDays(NormalizeRevalidationIntervalDays(revalidationIntervalDays)) <= utcNow;
    }

    private static DateTime? GetLastVerifiedUtc(PhoneNumberVerificationResult result)
    {
        if (result.VerificationDateUtc == default)
        {
            return null;
        }

        return result.Status is PhoneNumberVerificationStatus.Verified or PhoneNumberVerificationStatus.Invalid
            ? result.VerificationDateUtc
            : null;
    }

    private static int NormalizeRevalidationIntervalDays(int revalidationIntervalDays)
    {
        return revalidationIntervalDays > 0
            ? revalidationIntervalDays
            : PhoneNumberVerificationsSettings.DefaultRevalidationIntervalDays;
    }
}
