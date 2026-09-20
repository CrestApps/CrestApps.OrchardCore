using CrestApps.Core.Omnichannel.Models;

namespace CrestApps.Core.Omnichannel.Services;

/// <summary>
/// Processes subject actions when an activity is completed with a disposition.
/// </summary>
public interface ISubjectActionExecutor
{
    /// <summary>
    /// Executes all subject actions associated with the given subject content type and disposition.
    /// </summary>
    /// <param name="context">The subject action execution context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ExecuteAsync(SubjectActionExecutionContext context, CancellationToken cancellationToken = default);
}
