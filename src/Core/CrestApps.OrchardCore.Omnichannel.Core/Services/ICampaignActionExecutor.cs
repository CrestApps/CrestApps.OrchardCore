using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Processes campaign actions when an activity is completed with a disposition.
/// Replaces the workflow-based approach with direct action execution.
/// </summary>
public interface ICampaignActionExecutor
{
    /// <summary>
    /// Executes all campaign actions associated with the given campaign and disposition.
    /// </summary>
    /// <param name="context">The campaign action execution context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ExecuteAsync(CampaignActionExecutionContext context, CancellationToken cancellationToken = default);
}
