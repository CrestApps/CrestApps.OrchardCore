using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.PhoneNumbers.Core.Services;

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
        var attemptUtc = result.VerificationDateUtc == default
            ? null
            : (DateTime?)result.VerificationDateUtc;
        var requestCompleted = result.Status is PhoneNumberVerificationStatus.Verified or PhoneNumberVerificationStatus.Invalid;

        contentItem.Alter<PhoneNumberVerificationPart>(part =>
        {
            part.VerificationAttemptCount++;
            part.LastAttemptUtc = attemptUtc;

            if (requestCompleted)
            {
                var serializedResult = JsonSerializer.Serialize(result, PhoneNumberVerificationSerialization.Options);

                part.PhoneNumber = result.PhoneNumber;
                part.NormalizedPhoneNumber = result.NormalizedPhoneNumber ?? result.PhoneNumber;
                part.VerificationStatus = result.Status;
                part.VerificationProvider = result.VerificationProvider;
                part.VerificationResultJson = serializedResult;
                part.LastVerifiedByUserId = verifiedByUserId;
                part.LastVerifiedUtc = attemptUtc;
                part.NextVerificationDueUtc = attemptUtc?.AddDays(normalizedIntervalDays);
                part.FailedAttemptCount = 0;
                part.LastError = null;

                return;
            }

            // The verification request itself failed (provider rate limit, HTTP error, transport
            // failure, and so on). Record the failure without overwriting a previously known-good
            // result, so a transient provider outage never downgrades a verified number to invalid.
            part.FailedAttemptCount++;
            part.LastError = result.ErrorMessage;

            if (part.LastVerifiedUtc is null)
            {
                // The number has never completed verification, so surface the failure as the current
                // status and keep the failed payload available for diagnostics. The record stays due
                // (no next-due date) so the background task retries it until the attempt cap is reached.
                part.PhoneNumber = result.PhoneNumber ?? part.PhoneNumber;
                part.NormalizedPhoneNumber = result.NormalizedPhoneNumber ?? result.PhoneNumber ?? part.NormalizedPhoneNumber;
                part.VerificationStatus = PhoneNumberVerificationStatus.Failed;
                part.VerificationProvider = result.VerificationProvider;
                part.VerificationResultJson = JsonSerializer.Serialize(result, PhoneNumberVerificationSerialization.Options);
            }
        });
    }

    /// <summary>
    /// Re-queues a content item for verification by clearing the failure counters and due date and
    /// marking the record as pending (<see cref="PhoneNumberVerificationStatus.Unverified"/>) so it
    /// leaves the failed bucket immediately and is picked up by the background task again. The stored
    /// phone number is preserved.
    /// </summary>
    /// <param name="contentItem">The content item to re-queue.</param>
    public static void RequeuePhoneNumberVerification(this ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        contentItem.Alter<PhoneNumberVerificationPart>(part =>
        {
            part.VerificationStatus = PhoneNumberVerificationStatus.Unverified;
            part.FailedAttemptCount = 0;
            part.LastError = null;
            part.NextVerificationDueUtc = null;
        });
    }

    /// <summary>
    /// Determines whether a content item has reached the maximum number of consecutive failed
    /// verification attempts and therefore should no longer be retried automatically.
    /// </summary>
    /// <param name="contentItem">The content item to evaluate.</param>
    /// <param name="maxAttempts">The maximum number of consecutive failed attempts allowed.</param>
    /// <returns><see langword="true"/> when the failure cap has been reached; otherwise, <see langword="false"/>.</returns>
    public static bool HasReachedMaxVerificationAttempts(
        this ContentItem contentItem,
        int maxAttempts = PhoneNumberVerificationsSettings.DefaultMaxVerificationAttempts)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        return contentItem.TryGet<PhoneNumberVerificationPart>(out var part)
            && part.HasReachedMaxVerificationAttempts(maxAttempts);
    }

    /// <summary>
    /// Determines whether a verification part has reached the maximum number of consecutive failed
    /// verification attempts and therefore should no longer be retried automatically.
    /// </summary>
    /// <param name="part">The verification part to evaluate.</param>
    /// <param name="maxAttempts">The maximum number of consecutive failed attempts allowed.</param>
    /// <returns><see langword="true"/> when the failure cap has been reached; otherwise, <see langword="false"/>.</returns>
    public static bool HasReachedMaxVerificationAttempts(
        this PhoneNumberVerificationPart part,
        int maxAttempts = PhoneNumberVerificationsSettings.DefaultMaxVerificationAttempts)
    {
        ArgumentNullException.ThrowIfNull(part);

        return part.FailedAttemptCount >= NormalizeMaxAttempts(maxAttempts);
    }

    /// <summary>
    /// Stores an unverified phone number on a content item so it can be verified later.
    /// </summary>
    /// <param name="contentItem">The content item to update.</param>
    /// <param name="phoneNumber">The phone number to store.</param>
    /// <param name="normalizedPhoneNumber">The normalized phone number, when available.</param>
    public static void AlterPhoneNumberVerificationPending(
        this ContentItem contentItem,
        string phoneNumber,
        string normalizedPhoneNumber = null)
    {
        ArgumentNullException.ThrowIfNull(contentItem);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        contentItem.Alter<PhoneNumberVerificationPart>(part =>
        {
            part.PhoneNumber = phoneNumber;
            part.NormalizedPhoneNumber = normalizedPhoneNumber ?? phoneNumber;
            part.VerificationStatus = PhoneNumberVerificationStatus.Unverified;
            part.VerificationProvider = null;
            part.VerificationResultJson = null;
            part.LastVerifiedUtc = null;
            part.NextVerificationDueUtc = null;
            part.FailedAttemptCount = 0;
            part.LastError = null;
        });
    }

    /// <summary>
    /// Clears verification data when the content item no longer has a phone number.
    /// </summary>
    /// <param name="contentItem">The content item to update.</param>
    public static void ClearPhoneNumberVerification(this ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        contentItem.Alter<PhoneNumberVerificationPart>(part =>
        {
            part.PhoneNumber = null;
            part.NormalizedPhoneNumber = null;
            part.VerificationStatus = PhoneNumberVerificationStatus.Unverified;
            part.VerificationProvider = null;
            part.VerificationResultJson = null;
            part.LastVerifiedUtc = null;
            part.NextVerificationDueUtc = null;
            part.FailedAttemptCount = 0;
            part.LastError = null;
            part.LastAttemptUtc = null;
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

    private static int NormalizeRevalidationIntervalDays(int revalidationIntervalDays)
    {
        return revalidationIntervalDays > 0
            ? revalidationIntervalDays
            : PhoneNumberVerificationsSettings.DefaultRevalidationIntervalDays;
    }

    private static int NormalizeMaxAttempts(int maxAttempts)
    {
        return maxAttempts > 0
            ? maxAttempts
            : PhoneNumberVerificationsSettings.DefaultMaxVerificationAttempts;
    }
}
