using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Words the reason a record is not being dispatched for a report.
/// </summary>
internal static class DispatchBlockerLabels
{
    /// <summary>
    /// Describes a reason in the reader's language.
    /// </summary>
    /// <param name="localizer">The string localizer.</param>
    /// <param name="blocker">The reason.</param>
    /// <returns>The words for the reason.</returns>
    public static string Describe(IStringLocalizer localizer, DispatchBlocker blocker)
        => blocker switch
        {
            DispatchBlocker.DialableNow => localizer["Dialable now"].Value,
            DispatchBlocker.QueueDisabled => localizer["The queue is turned off"].Value,
            DispatchBlocker.BusinessHoursClosed => localizer["The queue's business hours are closed"].Value,
            DispatchBlocker.NotQueued => localizer["Not in the queue, so routing never sees it"].Value,
            DispatchBlocker.ProfileDisabled => localizer["The dialer profile is turned off"].Value,
            DispatchBlocker.ScheduledForLater => localizer["Scheduled for later"].Value,
            DispatchBlocker.RetryCoolDown => localizer["Waiting out the retry delay"].Value,
            DispatchBlocker.AttemptsExhausted => localizer["Out of attempts"].Value,
            DispatchBlocker.NoDestination => localizer["No number to dial"].Value,
            DispatchBlocker.DoNotCall => localizer["Do not call"].Value,
            DispatchBlocker.NationalDoNotCallRegistry => localizer["On the national do-not-call registry"].Value,
            DispatchBlocker.OutsideCallingWindow => localizer["Outside the calling window"].Value,
            DispatchBlocker.NumberNotInService => localizer["Number not in service"].Value,
            DispatchBlocker.ComplianceScreeningUnavailable => localizer["Compliance check unavailable"].Value,
            DispatchBlocker.AbandonmentRateExceeded => localizer["Abandonment rate cap reached"].Value,
            DispatchBlocker.ProfileMissing => localizer["The dialer profile it is stamped with no longer exists"].Value,
            _ => blocker.ToString(),
        };
}
