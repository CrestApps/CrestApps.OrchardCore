using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Records the activity an inbound call is worked on.
/// </summary>
public sealed partial class InboundVoiceCallProcessor
{
    private async Task<OmnichannelActivity> CreateActivityAsync(
        OmnichannelChannelEndpoint endpoint,
        SubjectFlowSettings flow,
        string fromAddress,
        IReadOnlyList<string> contactItemIds,
        DateTime now,
        string aiProfileId = null)
    {
        var activity = await _activityManager.NewAsync();
        activity.Kind = ActivityKind.Call;
        activity.Source = ActivitySources.Inbound;
        activity.Channel = OmnichannelConstants.Channels.Phone;
        activity.ChannelEndpointId = endpoint?.ItemId;
        activity.InteractionType = string.IsNullOrEmpty(aiProfileId) ? ActivityInteractionType.Manual : ActivityInteractionType.Automated;
        activity.AIProfileId = aiProfileId;
        activity.PreferredDestination = fromAddress;
        activity.CampaignId = flow?.CampaignId;
        activity.SubjectContentType = flow?.SubjectContentType;

        // An AI voice agent's call waits for the answer the conversation starts on, as a call the AI places does.
        activity.Status = string.IsNullOrEmpty(aiProfileId) ? ActivityStatus.AwaitingAgentResponse : ActivityStatus.AwaitingCustomerAnswer;
        activity.ScheduledUtc = now;
        activity.CreatedUtc = now;
        activity.ContactResolutionCandidates = contactItemIds.ToList();
        activity.ContactResolutionStatus = contactItemIds.Count switch
        {
            0 => ContactResolutionStatus.Unresolved,
            1 => ContactResolutionStatus.Resolved,
            _ => ContactResolutionStatus.Ambiguous,
        };

        if (activity.ContactResolutionStatus == ContactResolutionStatus.Resolved)
        {
            var contact = await _contentManager.GetAsync(contactItemIds[0]);

            if (contact is not null)
            {
                activity.ContactContentItemId = contact.ContentItemId;
                activity.ContactContentType = contact.ContentType;
                activity.ContactResolvedUtc = now;
            }
            else
            {
                activity.ContactResolutionStatus = ContactResolutionStatus.Unresolved;
            }
        }

        if (!string.IsNullOrEmpty(activity.SubjectContentType))
        {
            activity.Subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        // Routing state is created before the activity is persisted so the activity's read model is already
        // reconciled on its first write, instead of costing a second write to converge. A call an AI voice agent
        // answers is nobody's work to pick up, so it has none until the AI hands the caller to a person.
        if (string.IsNullOrEmpty(aiProfileId))
        {
            var workState = await _workStateService.MutateAsync(
                activity.ItemId,
                state => state.TransitionTo(ActivityAssignmentStatus.Available));

            if (workState is not null)
            {
                ContactCenterWorkStateProjector.Apply(activity, workState);
            }
        }

        await _activityManager.CreateAsync(activity);

        return activity;
    }
}
