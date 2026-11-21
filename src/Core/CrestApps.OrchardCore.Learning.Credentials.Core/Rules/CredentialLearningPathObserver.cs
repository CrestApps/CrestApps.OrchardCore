using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Rules;

/// <summary>
/// The learning path completed rule. A completed path counts only when the path service says it may support what is
/// issued on it: its rule holds with the courses whose completion is evidence and does not rest on a browser result.
/// </summary>
public sealed class CredentialLearningPathObserver : ILearningPathObserver
{
    private readonly CredentialRuleTrigger _trigger;
    private readonly ILearningPathService _paths;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialLearningPathObserver"/> class.
    /// </summary>
    /// <param name="trigger">The rule trigger.</param>
    /// <param name="paths">The learning path service, which says whether a completed path supports an award.</param>
    public CredentialLearningPathObserver(CredentialRuleTrigger trigger, ILearningPathService paths)
    {
        _trigger = trigger;
        _paths = paths;
    }

    /// <inheritdoc/>
    public async Task CompletedAsync(LearningPathProgress progress, CancellationToken cancellationToken = default)
    {
        if (progress is null || progress.CompletedUtc is null || !await _paths.SupportsIssuingAsync(progress, cancellationToken))
        {
            return;
        }

        await _trigger.TriggerAsync(CredentialsConstants.RuleKinds.LearningPathCompleted, progress.PathId, progress.UserId, progress.ItemId, cancellationToken);
    }
}
