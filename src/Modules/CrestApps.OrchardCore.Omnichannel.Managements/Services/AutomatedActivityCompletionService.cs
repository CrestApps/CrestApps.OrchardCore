using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Finalizes AI-driven activities through the shared activity disposition service.
/// </summary>
public sealed class AutomatedActivityCompletionService : IAutomatedActivityCompletionService
{
    private readonly IActivityDispositionService _activityDispositionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomatedActivityCompletionService"/> class.
    /// </summary>
    /// <param name="activityDispositionService">The source-neutral activity disposition service.</param>
    public AutomatedActivityCompletionService(IActivityDispositionService activityDispositionService)
    {
        _activityDispositionService = activityDispositionService;
    }

    /// <inheritdoc/>
    public Task<ActivityDispositionResult> CompleteAsync(
        AutomatedActivityCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Activity is null)
        {
            return Task.FromResult(ActivityDispositionResult.Failure("An activity is required to complete an automated conversation."));
        }

        if (string.IsNullOrWhiteSpace(request.AISessionId))
        {
            return Task.FromResult(ActivityDispositionResult.Failure("An AI session id is required to complete an automated conversation."));
        }

        request.Activity.AISessionId = request.AISessionId.Trim();

        return _activityDispositionService.ApplyAsync(new ActivityDispositionRequest
        {
            Activity = request.Activity,
            DispositionId = request.DispositionId,
            Source = ActivityDispositionSource.AI,
            Notes = request.Summary?.Trim(),
            ActionScheduleDates = request.ActionScheduleDates,
            ActorId = request.ActorId,
            ActorDisplayName = request.ActorDisplayName,
        }, cancellationToken);
    }
}
