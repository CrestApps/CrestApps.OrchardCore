using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Builds the activity that tries a contact again after an earlier activity for them was completed.
/// </summary>
public static class OmnichannelActivityFollowUps
{
    /// <summary>
    /// A new, not-started activity that repeats the completed one as its next attempt.
    /// </summary>
    /// <remarks>
    /// The retry is the same work tried again, so it is carried out the same way. Leaving the kind, source and automation
    /// settings behind turned a call that rang out into a manual task with no AI profile, which the automated processor
    /// picked up with nothing to place the call and the contact was never tried again. The AI session and re-engagement
    /// count are not copied: the retry is a new conversation. Its attempt number is the one after the completed
    /// activity's, so a dialer's attempt limit counts across the chain of activities. The caller sets the schedule and
    /// the owner.
    /// </remarks>
    /// <param name="activity">The completed activity to try again.</param>
    /// <param name="nowUtc">The current time, recorded as when the retry was created.</param>
    public static OmnichannelActivity CreateNextAttempt(OmnichannelActivity activity, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var nextAttempt = new OmnichannelActivity
        {
            ItemId = IdGenerator.GenerateId(),
            Kind = activity.Kind,
            Source = activity.Source,
            Channel = activity.Channel,
            ChannelEndpointId = activity.ChannelEndpointId,
            InteractionType = activity.InteractionType,
            AIProfileId = activity.AIProfileId,
            SpeechToTextDeploymentName = activity.SpeechToTextDeploymentName,
            TextToSpeechDeploymentName = activity.TextToSpeechDeploymentName,
            TextToSpeechVoiceId = activity.TextToSpeechVoiceId,
            UseCallAmbience = activity.UseCallAmbience,
            AllowAIToUpdateContact = activity.AllowAIToUpdateContact,
            AllowAIToUpdateSubject = activity.AllowAIToUpdateSubject,
            ResponseDelayMode = activity.ResponseDelayMode,
            ResponseDelaySeconds = activity.ResponseDelaySeconds,
            ResponseDelayJitterSeconds = activity.ResponseDelayJitterSeconds,
            BusinessHoursCalendarId = activity.BusinessHoursCalendarId,
            CadenceId = activity.CadenceId,
            PreferredDestination = activity.PreferredDestination,
            ContactContentItemId = activity.ContactContentItemId,
            ContactContentType = activity.ContactContentType,
            ContactResolutionStatus = activity.ContactResolutionStatus,
            ContactResolutionCandidates = activity.ContactResolutionCandidates.ToList(),
            ContactResolvedUtc = activity.ContactResolvedUtc,
            ContactResolvedById = activity.ContactResolvedById,
            ContactResolvedByUsername = activity.ContactResolvedByUsername,
            CampaignId = activity.CampaignId,
            Instructions = activity.Instructions,
            Attempts = activity.Attempts + 1,
            CreatedById = activity.CompletedById,
            CreatedByUsername = activity.CompletedByUsername,
            CreatedUtc = nowUtc,
            SubjectContentType = activity.SubjectContentType,
            Subject = activity.Subject,
            UrgencyLevel = activity.UrgencyLevel,
            Status = ActivityStatus.NotStated,
        };

        // Whether the AI may convert the lead is one of the automation settings the retry keeps.
        if (activity.TryGet<LeadAIConversionSettings>(out var leadAIConversion))
        {
            nextAttempt.Put(leadAIConversion);
        }

        return nextAttempt;
    }
}
