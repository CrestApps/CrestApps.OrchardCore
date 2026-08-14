using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Default provider-agnostic processor for queued phone number verification records.
/// </summary>
internal sealed class PhoneNumberVerificationQueueProcessor : IPhoneNumberVerificationQueueProcessor
{
    private readonly IPhoneNumberVerificationManager _verificationManager;
    private readonly IPhoneNumberVerificationRequestDelayer _requestDelayer;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationQueueProcessor"/> class.
    /// </summary>
    /// <param name="verificationManager">The verification manager.</param>
    /// <param name="requestDelayer">The provider-request delayer.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public PhoneNumberVerificationQueueProcessor(
        IPhoneNumberVerificationManager verificationManager,
        IPhoneNumberVerificationRequestDelayer requestDelayer,
        IClock clock,
        ILogger<PhoneNumberVerificationQueueProcessor> logger)
    {
        _verificationManager = verificationManager;
        _requestDelayer = requestDelayer;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> ProcessAsync(
        IEnumerable<ContentItem> contentItems,
        PhoneNumberVerificationsSettings settings,
        bool delayBeforeFirstRequest = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentItems);
        ArgumentNullException.ThrowIfNull(settings);

        var processed = 0;
        var shouldDelay = delayBeforeFirstRequest;
        var delayMilliseconds = NormalizeRequestDelayMilliseconds(settings.RequestDelayMilliseconds);

        foreach (var contentItem in contentItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var phoneNumber = GetStoredPhoneNumber(contentItem);

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                continue;
            }

            if (shouldDelay && delayMilliseconds > 0)
            {
                await _requestDelayer.DelayAsync(delayMilliseconds, cancellationToken);
            }

            shouldDelay = true;

            var result = await VerifyAsync(contentItem, phoneNumber, cancellationToken);

            contentItem.AlterPhoneNumberVerificationResult(
                result,
                revalidationIntervalDays: settings.RevalidationIntervalDays);

            if (OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumberContentItem(contentItem) is { } phoneNumberContentItem)
            {
                phoneNumberContentItem.AlterPhoneNumberVerificationResult(
                    result,
                    revalidationIntervalDays: settings.RevalidationIntervalDays);
            }

            processed++;
        }

        return processed;
    }

    internal static string GetStoredPhoneNumber(ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        if (!contentItem.TryGet<PhoneNumberVerificationPart>(out var part))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(part.NormalizedPhoneNumber))
        {
            return part.NormalizedPhoneNumber;
        }

        if (!string.IsNullOrWhiteSpace(part.PhoneNumber))
        {
            return part.PhoneNumber;
        }

        return part.TryGetPhoneNumberVerificationResult(out var result)
            ? result.NormalizedPhoneNumber ?? result.PhoneNumber
            : null;
    }

    private async Task<PhoneNumberVerificationResult> VerifyAsync(
        ContentItem contentItem,
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _verificationManager.VerifyAsync(phoneNumber, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to verify the phone number for content item '{ContentItemId}'.", contentItem.ContentItemId);

            return new PhoneNumberVerificationResult
            {
                PhoneNumber = phoneNumber,
                NormalizedPhoneNumber = phoneNumber,
                VerificationDateUtc = _clock.UtcNow,
                Status = PhoneNumberVerificationStatus.Failed,
                LineType = PhoneNumberLineType.Unknown,
                ErrorMessage = ex.Message,
            };
        }
    }

    private static int NormalizeRequestDelayMilliseconds(int delayMilliseconds)
    {
        if (delayMilliseconds >= 0)
        {
            return delayMilliseconds;
        }

        return PhoneNumberVerificationsSettings.DefaultRequestDelayMilliseconds;
    }
}
