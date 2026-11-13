using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Stores the progress of each enrollment in a document of its own.
/// </summary>
public sealed class EnrollmentProgressStore : DocumentCatalog<EnrollmentProgress, EnrollmentProgressIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentProgressStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public EnrollmentProgressStore(ISession session)
        : base(session)
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <summary>
    /// Finds the progress of an enrollment.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The progress, or <see langword="null"/> when the learner has not opened any item.</returns>
    public async Task<EnrollmentProgress> FindByEnrollmentAsync(string enrollmentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(enrollmentId))
        {
            return null;
        }

        return await Session.Query<EnrollmentProgress, EnrollmentProgressIndex>(index => index.EnrollmentId == enrollmentId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the progress of several enrollments.
    /// </summary>
    /// <param name="enrollmentIds">The enrollments.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The progress documents.</returns>
    public async Task<IReadOnlyList<EnrollmentProgress>> ListByEnrollmentsAsync(IReadOnlyCollection<string> enrollmentIds, CancellationToken cancellationToken = default)
    {
        if (enrollmentIds is null || enrollmentIds.Count == 0)
        {
            return [];
        }

        return (await Session.Query<EnrollmentProgress, EnrollmentProgressIndex>(index => index.EnrollmentId.IsIn(enrollmentIds), collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Saves progress.
    /// </summary>
    /// <param name="progress">The progress.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task SaveAsync(EnrollmentProgress progress, CancellationToken cancellationToken = default)
        => Session.SaveAsync(progress, false, CollectionName, cancellationToken);
}
