using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
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
    private readonly INotInServiceNumberService _notInServiceNumbers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationQueueProcessor"/> class.
    /// </summary>
    /// <param name="verificationManager">The verification manager.</param>
    /// <param name="requestDelayer">The provider-request delayer.</param>
    /// <param name="notInServiceNumbers">
    /// The list of numbers known not to be in service, when activity management is enabled to keep one.
    /// </param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public PhoneNumberVerificationQueueProcessor(
        IPhoneNumberVerificationManager verificationManager,
        IPhoneNumberVerificationRequestDelayer requestDelayer,
        IEnumerable<INotInServiceNumberService> notInServiceNumbers,
        IClock clock,
        ILogger<PhoneNumberVerificationQueueProcessor> logger)
    {
        _verificationManager = verificationManager;
        _requestDelayer = requestDelayer;
        _notInServiceNumbers = notInServiceNumbers.FirstOrDefault();
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

            await ApplyToNotInServiceNumbersAsync(contentItem, phoneNumber, result, cancellationToken);

            processed++;
        }

        return processed;
    }

    /// <summary>
    /// Keeps the list of numbers the dialer leaves out in step with what the lookup found.
    /// </summary>
    /// <remarks>
    /// A lookup that says a number is not a valid number, or that its line is inactive, has found a dead number
    /// before anybody dialed it, and it is marked so no campaign loads or dials it. Only those two answers mark a
    /// number: an "unreachable" line is a phone that is switched off or out of coverage, which is still somebody's
    /// number. A later lookup that finds the number verified again clears a mark an earlier lookup made, because the
    /// number has been given to somebody new; a mark made by an actual call is left for a person to clear.
    /// </remarks>
    private async Task ApplyToNotInServiceNumbersAsync(
        ContentItem contentItem,
        string phoneNumber,
        PhoneNumberVerificationResult result,
        CancellationToken cancellationToken)
    {
        if (_notInServiceNumbers is null || result is null)
        {
            return;
        }

        var number = result.NormalizedPhoneNumber ?? result.PhoneNumber ?? phoneNumber;

        if (result.Status == PhoneNumberVerificationStatus.Invalid && IsDeadNumber(result))
        {
            await _notInServiceNumbers.MarkAsync(new NotInServiceMark
            {
                PhoneNumber = number,
                Source = OmnichannelConstants.NotInServiceSources.Lookup,
                Reason = DescribeLookup(result),
                ContactContentItemId = contentItem.ContentItemId,
            }, cancellationToken);

            return;
        }

        if (result.Status == PhoneNumberVerificationStatus.Verified &&
            await _notInServiceNumbers.FindAsync(number, cancellationToken) is { } mark &&
            string.Equals(mark.Source, OmnichannelConstants.NotInServiceSources.Lookup, StringComparison.Ordinal))
        {
            await _notInServiceNumbers.ClearAsync(number, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "A new lookup verified '{PhoneNumber}', so the not-in-service mark an earlier lookup made was cleared.",
                    number.SanitizeLogValue());
            }
        }
    }

    private static bool IsDeadNumber(PhoneNumberVerificationResult result)
        => !result.IsValid ||
            string.Equals(result.LineStatus?.Trim(), "inactive", StringComparison.OrdinalIgnoreCase);

    private static string DescribeLookup(PhoneNumberVerificationResult result)
        => result.IsValid
            ? $"{result.VerificationProvider} lookup: line status {result.LineStatus}"
            : $"{result.VerificationProvider} lookup: not a valid number";

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
