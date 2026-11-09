using CrestApps.Core.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Runs an agent's shared skills sync as a deferred task: after the request's session is committed, in a scope and a
/// session of its own.
/// </summary>
internal sealed class DeferredAgentSkillsSyncScheduler : IAgentSkillsSyncScheduler
{
    private readonly HashSet<string> _scheduled = new(StringComparer.Ordinal);

    public void Schedule(string agentId)
    {
        if (string.IsNullOrEmpty(agentId) || !_scheduled.Add(agentId))
        {
            return;
        }

        ShellScope.AddDeferredTask(scope => RunAsync(scope.ServiceProvider, agentId));
    }

    private static async Task RunAsync(IServiceProvider services, string agentId)
    {
        try
        {
            await services.GetRequiredService<AgentSkillsSyncRunner>().RunAsync(agentId);
        }
        catch (Exception ex)
        {
            // The agent's own save is already committed; the next save of the agent tries the sync again.
            services.GetRequiredService<ILogger<DeferredAgentSkillsSyncScheduler>>().LogWarning(
                ex,
                "Could not sync the skills of Contact Center agent '{AgentId}' with the shared skill catalog.",
                agentId.SanitizeLogValue());
        }
    }
}
