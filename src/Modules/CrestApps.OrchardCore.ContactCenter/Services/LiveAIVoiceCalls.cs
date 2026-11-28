using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// A live call an AI voice agent is holding.
/// </summary>
/// <param name="Activity">The call's automated activity.</param>
/// <param name="Call">The live call.</param>
internal sealed record LiveAIVoiceCall(OmnichannelActivity Activity, AILiveCall Call);

/// <summary>
/// Reads the calls an AI voice agent is holding right now: automated phone activities in progress that were not
/// escalated to a person and carry a live call. The live dashboard and the supervision AI tools both read them here.
/// </summary>
internal static class LiveAIVoiceCalls
{
    /// <summary>
    /// Lists the newest live AI voice calls.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="max">The most calls to read.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The live calls, newest first.</returns>
    public static async Task<IReadOnlyList<LiveAIVoiceCall>> ListAsync(ISession session, int max, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        var activities = await session
            .Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.InteractionType == ActivityInteractionType.Automated &&
                    index.Status == ActivityStatus.InProgress &&
                    index.Channel == OmnichannelConstants.Channels.Phone &&
                    !index.AiEscalated,
                collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(index => index.ScheduledUtc)
            .Take(max)
            .ListAsync(cancellationToken);

        var calls = new List<LiveAIVoiceCall>();

        foreach (var activity in activities)
        {
            if (activity.TryGet<AILiveCall>(out var liveCall))
            {
                calls.Add(new LiveAIVoiceCall(activity, liveCall));
            }
        }

        return calls;
    }
}
