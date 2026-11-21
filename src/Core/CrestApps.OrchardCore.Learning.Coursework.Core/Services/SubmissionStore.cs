using CrestApps.OrchardCore.Learning.Coursework.Core.Indexes;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Coursework.Core.Services;

/// <summary>
/// Stores submissions in their own collection.
/// </summary>
public sealed class SubmissionStore : DocumentCatalog<Submission, SubmissionIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public SubmissionStore(ISession session)
        : base(session)
    {
        CollectionName = CourseworkConstants.CollectionName;
    }

    /// <summary>
    /// Reads a submission again from the store, leaving out any copy the session already holds, so a write under the
    /// submission lock sees what the last writer committed.
    /// </summary>
    /// <param name="submissionId">The submission.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submission, or <see langword="null"/>.</returns>
    public async Task<Submission> FindFreshAsync(string submissionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(submissionId))
        {
            return null;
        }

        var tracked = await FindByIdAsync(submissionId, cancellationToken);

        if (tracked is null)
        {
            return null;
        }

        Session.Detach(tracked, CollectionName);

        return await FindByIdAsync(submissionId, cancellationToken);
    }

    /// <summary>
    /// Lists the submissions of a coursework item under an enrollment, newest attempt first.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="courseworkId">The coursework item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submissions.</returns>
    public async Task<IReadOnlyList<Submission>> ListForEnrollmentAsync(string enrollmentId, string courseworkId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(enrollmentId) || string.IsNullOrEmpty(courseworkId)
            ? []
            : (await Session.Query<Submission, SubmissionIndex>(index => index.EnrollmentId == enrollmentId && index.CourseworkId == courseworkId, collection: CollectionName)
                .OrderByDescending(index => index.Attempt)
                .ThenByDescending(index => index.Id)
                .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Lists the submissions of a learner, newest first.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submissions.</returns>
    public async Task<IReadOnlyList<Submission>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userId)
            ? []
            : (await Session.Query<Submission, SubmissionIndex>(index => index.UserId == userId, collection: CollectionName)
                .OrderByDescending(index => index.SubmittedUtc)
                .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Reads a page of the submissions of a coursework item, newest first.
    /// </summary>
    /// <param name="courseworkId">The coursework item.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    public async Task<(int Count, IReadOnlyList<Submission> Items)> PageByCourseworkAsync(string courseworkId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(courseworkId))
        {
            return (0, []);
        }

        var query = Session.Query<Submission, SubmissionIndex>(index => index.CourseworkId == courseworkId, collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(index => index.SubmittedUtc).ThenByDescending(index => index.Id).Skip(Math.Max(skip, 0)).Take(Math.Max(take, 1)).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }

    /// <summary>
    /// Reads a page of the submissions that wait for a grader, oldest first, optionally of some courses.
    /// </summary>
    /// <param name="courseIds">The courses, or <see langword="null"/> for every course.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    public async Task<(int Count, IReadOnlyList<Submission> Items)> PageWaitingAsync(IReadOnlyCollection<string> courseIds, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<Submission, SubmissionIndex>(index => index.Waiting, collection: CollectionName);

        if (courseIds is not null)
        {
            var ids = courseIds.ToArray();

            if (ids.Length == 0)
            {
                return (0, []);
            }

            query = query.Where(index => index.CourseId.IsIn(ids));
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(index => index.SubmittedUtc).ThenBy(index => index.Id).Skip(Math.Max(skip, 0)).Take(Math.Max(take, 1)).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }

    /// <summary>
    /// Reads a page of submissions for an export or a data set, oldest first, optionally of one coursework item.
    /// </summary>
    /// <param name="courseworkId">The coursework item, or <see langword="null"/>.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    public async Task<IReadOnlyList<Submission>> PageForExportAsync(string courseworkId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<Submission, SubmissionIndex>(collection: CollectionName);

        if (!string.IsNullOrEmpty(courseworkId))
        {
            query = query.Where(index => index.CourseworkId == courseworkId);
        }

        return (await query.OrderBy(index => index.SubmittedUtc).ThenBy(index => index.Id).Skip(Math.Max(skip, 0)).Take(Math.Max(take, 1)).ListAsync(cancellationToken)).ToArray();
    }
}
