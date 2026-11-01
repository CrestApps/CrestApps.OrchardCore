using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services;

/// <summary>
/// Links contact center agents and workers through their user account: every agent with a user gets a worker, so it
/// can be scheduled, take time off and be paid like every other worker.
/// </summary>
public sealed class AgentWorkerService
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IWorkerManager _workerManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWorkerService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    public AgentWorkerService(IAgentProfileManager agentManager, IWorkerManager workerManager)
    {
        _agentManager = agentManager;
        _workerManager = workerManager;
    }

    /// <summary>
    /// Gets the agents, and which of them have a worker.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Every agent with a user, with its worker or <see langword="null"/>.</returns>
    public async Task<IReadOnlyList<(AgentProfile Agent, WorkerProfile Worker)>> ListAsync(CancellationToken cancellationToken = default)
    {
        var agents = (await _agentManager.GetAllAsync(cancellationToken))
            .Where(agent => !string.IsNullOrEmpty(agent.UserId))
            .OrderBy(agent => NameOf(agent), StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var result = new List<(AgentProfile, WorkerProfile)>(agents.Length);

        foreach (var agent in agents)
        {
            result.Add((agent, await _workerManager.FindByUserIdAsync(agent.UserId, cancellationToken)));
        }

        return result;
    }

    /// <summary>
    /// Gets the agent of a worker.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent, or <see langword="null"/> when the worker has no user or the user is not an agent.</returns>
    public async Task<AgentProfile> FindAgentAsync(WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        return string.IsNullOrEmpty(worker.UserId) ? null : await _agentManager.FindByUserIdAsync(worker.UserId, cancellationToken);
    }

    /// <summary>
    /// Gets the worker of an agent, creating one when there is none.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="teamId">The team a new worker joins, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The worker and whether it was created; no worker when the agent has no user or one can't be created.</returns>
    public async Task<(WorkerProfile Worker, bool Created)> EnsureWorkerAsync(AgentProfile agent, string teamId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (string.IsNullOrEmpty(agent.UserId))
        {
            return (null, false);
        }

        var existing = await _workerManager.FindByUserIdAsync(agent.UserId, cancellationToken);

        if (existing is not null)
        {
            return (existing, false);
        }

        var worker = await _workerManager.NewAsync(cancellationToken: cancellationToken);

        worker.DisplayName = NameOf(agent);
        worker.UserId = agent.UserId;
        worker.JobTitle = "Agent";

        if (!string.IsNullOrEmpty(teamId))
        {
            worker.PrimaryTeamId = teamId;
            worker.TeamMemberships.Add(new TeamMembership { TeamId = teamId });
        }

        var validation = await _workerManager.ValidateAsync(worker, cancellationToken);

        if (!validation.Succeeded)
        {
            return (null, false);
        }

        await _workerManager.CreateAsync(worker, cancellationToken);

        return (worker, true);
    }

    /// <summary>
    /// Creates a worker for every agent that has none.
    /// </summary>
    /// <param name="teamId">The team the new workers join, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many workers were created.</returns>
    public async Task<int> CreateMissingAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var created = 0;

        foreach (var (agent, worker) in await ListAsync(cancellationToken))
        {
            if (worker is null && (await EnsureWorkerAsync(agent, teamId, cancellationToken)).Created)
            {
                created++;
            }
        }

        return created;
    }

    private static string NameOf(AgentProfile agent)
        => !string.IsNullOrWhiteSpace(agent.DisplayName) ? agent.DisplayName
            : !string.IsNullOrWhiteSpace(agent.UserName) ? agent.UserName
            : agent.Name;
}
