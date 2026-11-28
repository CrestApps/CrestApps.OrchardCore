namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Tells an AI tool whether a record belongs to the assistant running the current AI turn, which a turn may not change.
/// The AI Agent module implements it with its configuration guard, so tools in other modules (file sources, web
/// crawlers) can apply the same rule without using a type from that module. When it is not registered, nothing is
/// protected.
/// </summary>
public interface IAIRunningAssistantGuard
{
    /// <summary>
    /// Whether the assistant running the current AI turn answers from an AI data source, through its profile, its agents,
    /// a chat interaction or a data source search tool instance. Outside an AI turn nothing is protected.
    /// </summary>
    /// <param name="dataSourceId">The ID of the data source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the turn may not change what feeds the data source.</returns>
    Task<bool> IsProtectedDataSourceAsync(string dataSourceId, CancellationToken cancellationToken = default);
}
