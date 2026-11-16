using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Rules;

/// <summary>
/// The learning path completed rule.
/// </summary>
public sealed class CredentialLearningPathObserver : ILearningPathObserver
{
    private readonly CredentialRuleTrigger _trigger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialLearningPathObserver"/> class.
    /// </summary>
    /// <param name="trigger">The rule trigger.</param>
    public CredentialLearningPathObserver(CredentialRuleTrigger trigger)
    {
        _trigger = trigger;
    }

    /// <inheritdoc/>
    public Task CompletedAsync(LearningPathProgress progress, CancellationToken cancellationToken = default)
    {
        if (progress is null || progress.CompletedUtc is null)
        {
            return Task.CompletedTask;
        }

        return _trigger.TriggerAsync(CredentialsConstants.RuleKinds.LearningPathCompleted, progress.PathId, progress.UserId, progress.ItemId, cancellationToken);
    }
}
