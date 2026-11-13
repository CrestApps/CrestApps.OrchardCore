namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Hears every export of learner records, which hold personal data, so it can be recorded, as the Audit Trail does.
/// </summary>
public interface ILearnerRecordExportObserver
{
    /// <summary>
    /// Learner records were exported.
    /// </summary>
    /// <param name="kind">What was exported, such as <c>LearningEnrollments</c>.</param>
    /// <param name="count">How many records.</param>
    /// <param name="actor">The user id of the person who exported them.</param>
    /// <param name="filter">A description of the filter of the export.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ExportedAsync(string kind, int count, string actor, string filter, CancellationToken cancellationToken = default);
}
