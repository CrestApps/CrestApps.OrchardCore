using CrestApps.OrchardCore.Learning.Programs.Models;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Hears what happens to people under required learning, after it is saved. Register one with
/// <c>services.AddRequiredLearningObserver&lt;T&gt;()</c>.
/// </summary>
public interface IRequiredLearningObserver
{
    /// <summary>
    /// Called after learning is required of a person.
    /// </summary>
    Task RequiredAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called when required learning is due in some days.
    /// </summary>
    Task DueSoonAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, int daysBefore, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called when required learning becomes overdue.
    /// </summary>
    Task OverdueAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called when overdue required learning is escalated to the person's managers.
    /// </summary>
    Task EscalatedAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, IReadOnlyList<string> managerUserIds, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after a person is exempted.
    /// </summary>
    Task ExemptedAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after a person completes required learning.
    /// </summary>
    Task CompletedAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
