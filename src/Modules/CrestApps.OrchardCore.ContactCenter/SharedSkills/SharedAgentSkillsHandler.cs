using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// When a user's shared skills change, updates the routing proficiencies of their agent profile, if they have one.
/// </summary>
/// <remarks>
/// The assignment service constructs every <see cref="ISkillAssignmentHandler"/>, and the agent profile manager and
/// the sync both lead back to the assignment service, so this handler resolves them when it runs rather than in its
/// constructor.
/// </remarks>
internal sealed class SharedAgentSkillsHandler : ISkillAssignmentHandler
{
    private readonly IServiceProvider _serviceProvider;

    public SharedAgentSkillsHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task AssignmentsChangedAsync(SkillAssignmentsChangedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Subject.Kind != SkillSubjectKind.User || context.Subject.IsEmpty)
        {
            return;
        }

        var sync = _serviceProvider.GetRequiredService<AgentSkillsSync>();

        if (sync.Syncing)
        {
            return;
        }

        var agentProfileManager = _serviceProvider.GetRequiredService<IAgentProfileManager>();
        var profile = await agentProfileManager.FindByUserIdAsync(context.Subject.Id, cancellationToken);

        if (profile is null)
        {
            return;
        }

        await sync.RunSilentlyAsync(async () =>
        {
            var assignments = context.Assignments;

            // An agent never matched with the catalog first adds its own skills, so none are lost.
            if (!AgentSkillsSync.HasSynced(profile))
            {
                await sync.PushAsync(profile, removeMissing: false, cancellationToken);
                assignments = await _serviceProvider.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(context.Subject, cancellationToken);
            }

            await sync.PullAsync(profile, assignments, cancellationToken);
            await agentProfileManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        });
    }
}
