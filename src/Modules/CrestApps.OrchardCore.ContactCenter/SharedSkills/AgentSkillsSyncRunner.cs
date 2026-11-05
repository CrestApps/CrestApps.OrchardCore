using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Brings one agent and the user's shared skills in step, and saves the agent marked as in step.
/// </summary>
internal sealed class AgentSkillsSyncRunner
{
    private readonly AgentSkillsSync _sync;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly ISkillAssignmentService _assignmentService;

    public AgentSkillsSyncRunner(
        AgentSkillsSync sync,
        IAgentProfileManager agentProfileManager,
        ISkillAssignmentService assignmentService)
    {
        _sync = sync;
        _agentProfileManager = agentProfileManager;
        _assignmentService = assignmentService;
    }

    /// <summary>
    /// Syncs the agent with the specified id, read afresh.
    /// </summary>
    /// <returns><see langword="true"/> when the agent was synced; <see langword="false"/> when it was already in step.</returns>
    public async Task<bool> RunAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var profile = await _agentProfileManager.FindByIdAsync(agentId, cancellationToken);

        return profile is not null && await RunAsync(profile, cancellationToken);
    }

    /// <summary>
    /// Syncs the specified agent.
    /// </summary>
    /// <returns><see langword="true"/> when the agent was synced; <see langword="false"/> when it was already in step.</returns>
    public async Task<bool> RunAsync(AgentProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrEmpty(profile.UserId) || AgentSkillsSync.IsInStep(profile))
        {
            return false;
        }

        await _sync.RunSilentlyAsync(async () =>
        {
            if (!AgentSkillsSync.HasSynced(profile) && profile.SkillProficiencies is not { Count: > 0 })
            {
                // A new agent with no proficiencies of its own takes the user's shared skills.
                await _sync.PullAsync(profile, await _assignmentService.GetAssignmentsAsync(SkillSubject.User(profile.UserId), cancellationToken), cancellationToken);
            }
            else
            {
                // A profile saved before the catalog was shared only adds its skills; later saves are the agent's full set.
                await _sync.PushAsync(profile, removeMissing: AgentSkillsSync.HasSynced(profile), cancellationToken);
            }

            await _agentProfileManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        });

        return true;
    }
}
