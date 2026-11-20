using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Rules;

/// <summary>
/// The course completed rule. A completion counts only when Learning's completion evidence rules let it: not failed,
/// not under review, and not resting on a result the browser reported for an item that does not allow it.
/// </summary>
public sealed class CredentialEnrollmentObserver : IEnrollmentObserver
{
    private readonly CredentialRuleTrigger _trigger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialEnrollmentObserver"/> class.
    /// </summary>
    /// <param name="trigger">The rule trigger.</param>
    public CredentialEnrollmentObserver(CredentialRuleTrigger trigger)
    {
        _trigger = trigger;
    }

    /// <inheritdoc/>
    public Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => TriggerAsync(enrollment, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>The award service keeps one award per rule and source, so a completion awarded before its review is not
    /// awarded again.</remarks>
    public Task ReviewClearedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => TriggerAsync(enrollment, cancellationToken);

    private Task TriggerAsync(Enrollment enrollment, CancellationToken cancellationToken)
    {
        if (!CompletionEvidence.SupportsIssuing(enrollment))
        {
            return Task.CompletedTask;
        }

        return _trigger.TriggerAsync(CredentialsConstants.RuleKinds.CourseCompleted, enrollment.CourseId, enrollment.UserId, enrollment.ItemId, cancellationToken);
    }
}
