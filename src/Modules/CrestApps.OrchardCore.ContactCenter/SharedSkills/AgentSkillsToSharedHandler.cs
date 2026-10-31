using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// When an agent profile is saved with different proficiencies, writes them to the user's shared skills. A new agent
/// with no proficiencies of its own takes the user's shared skills instead.
/// </summary>
internal sealed class AgentSkillsToSharedHandler : CatalogEntryHandlerBase<AgentProfile>
{
    private readonly AgentSkillsSync _sync;
    private readonly ISkillAssignmentService _assignmentService;

    public AgentSkillsToSharedHandler(
        AgentSkillsSync sync,
        ISkillAssignmentService assignmentService)
    {
        _sync = sync;
        _assignmentService = assignmentService;
    }

    public override async Task CreatingAsync(CreatingContext<AgentProfile> context, CancellationToken cancellationToken = default)
    {
        var profile = context.Model;

        if (_sync.Syncing || string.IsNullOrEmpty(profile.UserId))
        {
            return;
        }

        if (profile.SkillProficiencies is { Count: > 0 })
        {
            await _sync.PushAsync(profile, removeMissing: false, cancellationToken);

            return;
        }

        await _sync.PullAsync(profile, await _assignmentService.GetAssignmentsAsync(SkillSubject.User(profile.UserId), cancellationToken), cancellationToken);
    }

    public override Task UpdatingAsync(UpdatingContext<AgentProfile> context, CancellationToken cancellationToken = default)
    {
        var profile = context.Model;

        if (_sync.Syncing || string.IsNullOrEmpty(profile.UserId) || AgentSkillsSync.IsInStep(profile))
        {
            return Task.CompletedTask;
        }

        // A profile saved before the catalog was shared only adds its skills; later saves are the agent's full set.
        return _sync.PushAsync(profile, removeMissing: AgentSkillsSync.HasSynced(profile), cancellationToken);
    }
}
