using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Telemetry;

/// <summary>
/// The source-generated log messages of phone number verification, with event ids from
/// <see cref="CrestAppsEventIds.Telephony"/> + 800 to 999. The provider log messages keep the 1000s they already use.
/// </summary>
internal static partial class PhoneNumberVerificationLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.Telephony + 801,
        Level = LogLevel.Warning,
        Message = "Verifying the phone number of content item '{ContentItemId}' again after a failed lookup (attempt {Attempt}).")]
    public static partial void RetryingVerification(ILogger logger, string contentItemId, int attempt);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Telephony + 802,
        Level = LogLevel.Information,
        Message = "A new lookup verified '{PhoneNumber}', so the not-in-service mark an earlier lookup made was cleared.")]
    public static partial void NotInServiceMarkCleared(ILogger logger, string phoneNumber);
}
