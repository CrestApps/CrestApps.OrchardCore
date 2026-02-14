namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// A fluent builder for configuring an orchestrator registration.
/// </summary>
/// <typeparam name="TOrchestrator">The orchestrator type implementing <see cref="IOrchestrator"/>.</typeparam>
public sealed class OrchestratorBuilder<TOrchestrator>
    where TOrchestrator : class, IOrchestrator
{
    private readonly OrchestratorEntry _entry;

    internal OrchestratorBuilder(OrchestratorEntry entry)
    {
        _entry = entry;
    }

    /// <summary>
    /// Sets the localized display title for this orchestrator.
    /// If not set, the orchestrator name is used in the UI.
    /// </summary>
    public OrchestratorBuilder<TOrchestrator> WithTitle(string title)
    {
        _entry.Title = title;
        return this;
    }
}
