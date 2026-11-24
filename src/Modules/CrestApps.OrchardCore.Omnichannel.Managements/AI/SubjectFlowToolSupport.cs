using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Describes a subject flow and its actions for the subject flow tools, from an explicit list of fields.
/// </summary>
internal static class SubjectFlowToolSupport
{
    /// <summary>
    /// The values a do-not-contact argument takes: set the flag on the contact, clear it, or leave it as it is.
    /// </summary>
    public static readonly string[] FlagChoices = ["set", "clear", "leave"];

    /// <summary>
    /// Describes a subject flow's settings without the AI prompts, which are summarized as flags.
    /// </summary>
    public static object DescribeFlow(SubjectFlowSettings flow)
        => flow is null
            ? null
            : new
            {
                direction = flow.Direction.ToString(),
                interactionType = flow.InteractionType.ToString(),
                channel = flow.Channel,
                channelEndpointId = flow.ChannelEndpointId,
                campaignId = flow.CampaignId,
                requireDisposition = flow.RequireDisposition,
                aiProfileId = flow.ProfileId,
                hasInitialOutboundPrompt = !string.IsNullOrWhiteSpace(flow.InitialOutboundPromptPattern),
                hasSubjectGoal = !string.IsNullOrWhiteSpace(flow.SubjectGoal),
                allowAIToUpdateContact = flow.AllowAIToUpdateContact,
                allowAIToUpdateSubject = flow.AllowAIToUpdateSubject,
                noResponseTimeoutInMinutes = flow.NoResponseTimeoutInMinutes,
                smsOptOutKeywords = flow.SmsOptOutKeywords ?? [],
                enableAgentHandoff = flow.EnableAgentHandoff,
                handoffQueueId = flow.HandoffQueueId,
                handoffOnUserRequest = flow.HandoffOnUserRequest,
                handoffOnQualifiedLead = flow.HandoffOnQualifiedLead,
                handoffOnFrustration = flow.HandoffOnFrustration,
            };

    /// <summary>
    /// Describes one action of a subject flow: what triggers it, what it does and the do-not-contact flags it sets.
    /// </summary>
    public static object DescribeAction(SubjectAction action, string dispositionName, string typeDisplayName)
    {
        object settings = null;

        if (string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase) &&
            action.TryGet<TryAgainActionMetadata>(out var tryAgain))
        {
            settings = new
            {
                maxAttempts = tryAgain.MaxAttempt,
                urgencyLevel = tryAgain.UrgencyLevel?.ToString(),
                assignmentType = tryAgain.AssignmentType.ToString(),
                ownerUserName = tryAgain.NormalizedUserName,
                defaultScheduleHours = tryAgain.DefaultScheduleHours,
            };
        }
        else if (string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase) &&
            action.TryGet<NewActivityActionMetadata>(out var newActivity))
        {
            settings = new
            {
                subjectContentType = newActivity.SubjectContentType,
                urgencyLevel = newActivity.UrgencyLevel?.ToString(),
                assignmentType = newActivity.AssignmentType.ToString(),
                ownerUserName = newActivity.NormalizedUserName,
                defaultScheduleHours = newActivity.DefaultScheduleHours,
            };
        }
        else if (string.Equals(action.Source, OmnichannelConstants.ActionTypes.ConvertLead, StringComparison.OrdinalIgnoreCase) &&
            action.TryGet<ConvertLeadActionMetadata>(out var convert))
        {
            settings = new
            {
                accountMode = convert.AccountMode.ToString(),
                createOpportunity = convert.CreateOpportunity,
                opportunityContentType = convert.OpportunityContentType,
                openActivities = convert.OpenActivities.ToString(),
            };
        }

        return new
        {
            id = action.ItemId,
            actionType = action.Source,
            actionTypeDisplayName = typeDisplayName ?? action.Source,
            subjectContentType = action.SubjectContentType,
            dispositionId = action.DispositionId,
            dispositionName = dispositionName ?? action.DispositionId,
            dispositionGuidance = action.DispositionGuidance,
            doNotCall = DescribeFlag(action.SetDoNotCall),
            doNotSms = DescribeFlag(action.SetDoNotSms),
            doNotEmail = DescribeFlag(action.SetDoNotEmail),
            setLeadStatusId = action.TryGet<SetLeadStatusActionMetadata>(out var leadStatus) ? leadStatus.StatusId : null,
            settings,
        };
    }

    /// <summary>
    /// Reads a do-not-contact choice: set, clear or leave.
    /// </summary>
    /// <returns><see langword="false"/> when the text is given but is none of the choices.</returns>
    public static bool TryReadFlag(string text, out bool? value, out bool given)
    {
        value = null;
        given = text is not null;

        if (text is null)
        {
            return true;
        }

        switch (text.Trim().ToLowerInvariant())
        {
            case "set":
                value = true;

                return true;

            case "clear":
                value = false;

                return true;

            case "leave":
                return true;

            default:
                return false;
        }
    }

    private static string DescribeFlag(bool? value)
        => value switch
        {
            true => "set",
            false => "clear",
            _ => "leave",
        };
}
