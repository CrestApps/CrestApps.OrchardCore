using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Shows the agent completing an activity what the AI assistant said about the conversation when it handed the
/// customer over, so the agent does not have to ask the customer to repeat themselves.
/// </summary>
/// <remarks>
/// Read-only and above the activity information, where the agent looks first. It is not part of the notes, which are
/// the agent's own and are written in the same form.
/// </remarks>
internal sealed class OmnichannelActivityHandoffSummaryDisplayDriver : DisplayDriver<OmnichannelActivity>
{
    private readonly ILocalClock _localClock;

    public OmnichannelActivityHandoffSummaryDisplayDriver(ILocalClock localClock)
    {
        _localClock = localClock;
    }

    public override IDisplayResult Edit(OmnichannelActivity activity, BuildEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(activity.HandoffSummary))
        {
            return null;
        }

        var summary = Initialize<OmnichannelActivityHandoffSummaryViewModel>("OmnichannelActivityHandoffSummary_Edit", async model =>
        {
            model.Summary = activity.HandoffSummary.Trim();
            model.WrittenLocal = activity.HandoffSummaryUtc.HasValue
                ? (await _localClock.ConvertToLocalAsync(activity.HandoffSummaryUtc.Value)).DateTime
                : null;
        }).Location("Content:4")
        .OnGroup(OmnichannelConstants.CompleteActivityGroup);

        // A completed activity is shown outside the completion group, and its summary still explains the call.
        if (activity.Status == ActivityStatus.Completed)
        {
            summary.OnGroup(string.Empty);
        }

        return summary;
    }
}
