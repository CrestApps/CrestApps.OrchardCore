using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Words how a campaign queue is paced for a report, in the same words the live dashboard uses.
/// </summary>
internal static class DispatchPacingLabels
{
    /// <summary>
    /// Describes how a queue is paced in the reader's language.
    /// </summary>
    /// <param name="localizer">The string localizer.</param>
    /// <param name="pacing">How the queue is paced.</param>
    /// <returns>The words for it.</returns>
    public static string Describe(IStringLocalizer localizer, DispatchPacing pacing)
        => pacing switch
        {
            DispatchPacing.NothingWaiting => localizer["Nothing waiting"].Value,
            DispatchPacing.Paced => localizer["Paced by the system"].Value,
            DispatchPacing.ProfileDisabled => localizer["Not paced: the dialer profile at the head of the queue is turned off"].Value,
            DispatchPacing.AgentDriven => localizer["Not paced by the system: agents work these records"].Value,
            DispatchPacing.ProfileMissing => localizer["Not paced: the item at the head of the queue has no dialer profile, or its profile no longer exists"].Value,
            _ => pacing.ToString(),
        };
}
