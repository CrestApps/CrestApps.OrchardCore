using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// When an agent profile is saved with different proficiencies, has them written to the user's shared skills. A new
/// agent with no proficiencies of its own takes the user's shared skills instead.
/// </summary>
/// <remarks>
/// The sync runs after the save, in a scope of its own (<see cref="IAgentSkillsSyncScheduler"/>), never inside it. A
/// failed query cancels the whole YesSql session, so a sync that failed inside the save threw away everything the
/// request wrote: with the Skills tables missing after a timed-out migration, every agent state change, release and
/// recovery was silently lost, and the agents stayed stuck.
/// </remarks>
internal sealed class AgentSkillsToSharedHandler : CatalogEntryHandlerBase<AgentProfile>
{
    private readonly AgentSkillsSync _sync;
    private readonly IAgentSkillsSyncScheduler _scheduler;

    public AgentSkillsToSharedHandler(
        AgentSkillsSync sync,
        IAgentSkillsSyncScheduler scheduler)
    {
        _sync = sync;
        _scheduler = scheduler;
    }

    public override Task CreatedAsync(CreatedContext<AgentProfile> context, CancellationToken cancellationToken = default)
    {
        Schedule(context.Model);

        return Task.CompletedTask;
    }

    public override Task UpdatedAsync(UpdatedContext<AgentProfile> context, CancellationToken cancellationToken = default)
    {
        Schedule(context.Model);

        return Task.CompletedTask;
    }

    private void Schedule(AgentProfile profile)
    {
        if (_sync.Syncing ||
            string.IsNullOrEmpty(profile?.UserId) ||
            string.IsNullOrEmpty(profile.ItemId) ||
            AgentSkillsSync.IsInStep(profile))
        {
            return;
        }

        _scheduler.Schedule(profile.ItemId);
    }
}
