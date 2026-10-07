namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// One definition of who dispositioned an activity: how a disposition request maps to an
/// <see cref="ActivityDispositionActor"/>, how the actor is stamped on the activity, and how it is inferred for
/// activities completed before the actor was stored.
/// </summary>
public static class ActivityDispositionActors
{
    /// <summary>
    /// Determines the actor behind a disposition request.
    /// </summary>
    /// <param name="request">The disposition request.</param>
    /// <returns>
    /// The actor the request names explicitly, otherwise the one its <see cref="ActivityDispositionRequest.Source"/>
    /// (and, for a number found not in service, what found it) stands for.
    /// </returns>
    public static ActivityDispositionActor FromRequest(ActivityDispositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DispositionedBy is { } explicitActor && explicitActor != ActivityDispositionActor.Unknown)
        {
            return explicitActor;
        }

        // What found a number dead says who ended the activity: the dialer's call, or the AI agent's own call.
        if (!string.IsNullOrEmpty(request.NotInServiceSource))
        {
            if (string.Equals(request.NotInServiceSource, OmnichannelConstants.NotInServiceSources.Dialer, StringComparison.Ordinal))
            {
                return ActivityDispositionActor.Dialer;
            }

            if (string.Equals(request.NotInServiceSource, OmnichannelConstants.NotInServiceSources.AutomatedCall, StringComparison.Ordinal))
            {
                return ActivityDispositionActor.AIAgent;
            }
        }

        return request.Source switch
        {
            ActivityDispositionSource.Agent => ActivityDispositionActor.User,
            ActivityDispositionSource.AI => ActivityDispositionActor.AIAgent,
            _ => ActivityDispositionActor.System,
        };
    }

    /// <summary>
    /// Records the actor on the activity, together with the AI profile when the AI agent concluded it, so the
    /// profile shown later is the one that handled the conversation.
    /// </summary>
    /// <param name="activity">The activity being completed.</param>
    /// <param name="actor">The actor that completed it.</param>
    public static void Stamp(OmnichannelActivity activity, ActivityDispositionActor actor)
    {
        ArgumentNullException.ThrowIfNull(activity);

        activity.DispositionedBy = actor;
        activity.DispositionedByAIProfileId = actor == ActivityDispositionActor.AIAgent
            ? activity.AIProfileId
            : null;
    }

    /// <summary>
    /// Gets the actor that dispositioned the activity: the stored one, or for an activity completed before it was
    /// stored, the best inference from what the activity does record.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The actor; never <see cref="ActivityDispositionActor.Unknown"/>.</returns>
    /// <remarks>
    /// The inference, in order: an automated (AI) activity was concluded by its AI agent, because an escalated call
    /// stops being automated; one with a completing user was a person; one loaded for a dialer with nobody recorded
    /// was the dialer; anything else was the platform.
    /// </remarks>
    public static ActivityDispositionActor Resolve(OmnichannelActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (activity.DispositionedBy != ActivityDispositionActor.Unknown)
        {
            return activity.DispositionedBy;
        }

        if (activity.InteractionType == ActivityInteractionType.Automated)
        {
            return ActivityDispositionActor.AIAgent;
        }

        if (!string.IsNullOrEmpty(activity.CompletedById))
        {
            return ActivityDispositionActor.User;
        }

        if (ActivitySources.IsDialer(activity.Source))
        {
            return ActivityDispositionActor.Dialer;
        }

        return ActivityDispositionActor.System;
    }

    /// <summary>
    /// Gets the AI profile that concluded the activity: the one stamped at completion, falling back to the
    /// activity's own profile for activities completed before it was stamped.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The AI profile identifier, or <see langword="null"/> when the AI agent did not conclude it.</returns>
    public static string ResolveAIProfileId(OmnichannelActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (Resolve(activity) != ActivityDispositionActor.AIAgent)
        {
            return null;
        }

        return string.IsNullOrEmpty(activity.DispositionedByAIProfileId)
            ? activity.AIProfileId
            : activity.DispositionedByAIProfileId;
    }
}
