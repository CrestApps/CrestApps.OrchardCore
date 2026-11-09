namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Runs the shared skills sync of an agent once the save that changed the agent is over.
/// </summary>
internal interface IAgentSkillsSyncScheduler
{
    /// <summary>
    /// Schedules the sync of the agent, once per agent for the current scope.
    /// </summary>
    /// <param name="agentId">The agent profile id.</param>
    void Schedule(string agentId);
}
