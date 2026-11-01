using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Handlers;

/// <summary>
/// Gives a new agent a worker. Agents saved before, or given a user later, get theirs from the Agents page of
/// Workforce, so the frequent presence saves of an agent don't look the worker up.
/// </summary>
/// <remarks>
/// The worker service is built when it is needed, not when the handler is built, so the agent manager that runs the
/// handler doesn't have to build the worker manager first.
/// </remarks>
internal sealed class AgentWorkerHandler : CatalogEntryHandlerBase<AgentProfile>
{
    private readonly Lazy<AgentWorkerService> _agentWorkerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWorkerHandler"/> class.
    /// </summary>
    /// <param name="agentWorkerService">The agent and worker link, built on first use.</param>
    public AgentWorkerHandler(Lazy<AgentWorkerService> agentWorkerService)
    {
        _agentWorkerService = agentWorkerService;
    }

    /// <inheritdoc/>
    public override async Task CreatedAsync(CreatedContext<AgentProfile> context, CancellationToken cancellationToken = default)
    {
        await _agentWorkerService.Value.EnsureWorkerAsync(context.Model, cancellationToken: cancellationToken);
    }
}
