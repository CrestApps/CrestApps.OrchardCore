using System.Text.Json.Serialization;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Teams.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Team members who are Contact Center agents and can't take work now: signed out, on a break, away, in a meeting or
/// training, not to be disturbed, or past their hours. An agent who is available, reserved for a call, on a call or
/// wrapping one up can still be given work such as a ticket; a user who isn't an agent is left to the other sources.
/// </summary>
public sealed class AgentPresenceAvailabilitySource : ITeamMemberAvailabilitySource
{
    private static readonly AgentPresenceStatus[] _canTakeWork = [AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp];

    private readonly ISession _session;

    /// <inheritdoc/>
    public string Name => "ContactCenterPresence";

    /// <inheritdoc/>
    [JsonIgnore]
    public global::OrchardCore.Localization.LocalizationSource DisplayName { get; } = global::OrchardCore.Localization.LocalizationSource.Create<AgentPresenceAvailabilitySource>("Signed in to Contact Center and not on a break");

    /// <inheritdoc/>
    /// <remarks>Off by default: people who work tickets but never sign in to the phone would otherwise never get any.</remarks>
    public bool HonoredByDefault => false;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPresenceAvailabilitySource"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public AgentPresenceAvailabilitySource(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetUnavailableAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (ids.Length == 0)
        {
            return [];
        }

        var agents = await _session.QueryIndex<AgentProfileIndex>(index => index.UserId.IsIn(ids), collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return [.. agents
            .Where(agent => !string.IsNullOrEmpty(agent.UserId) && !_canTakeWork.Contains(agent.PresenceStatus))
            .Select(agent => agent.UserId)
            .Distinct(StringComparer.Ordinal)];
    }
}
