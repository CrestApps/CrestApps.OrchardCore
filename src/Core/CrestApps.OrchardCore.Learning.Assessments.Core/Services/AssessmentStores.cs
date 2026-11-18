using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Stores attempts.
/// </summary>
public sealed class AssessmentAttemptStore : DocumentCatalog<AssessmentAttempt, AssessmentAttemptIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentAttemptStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public AssessmentAttemptStore(ISession session)
        : base(session)
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <summary>
    /// Lists the attempts of a learner on a quiz, oldest first.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="quizId">The quiz.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attempts.</returns>
    public async Task<IReadOnlyList<AssessmentAttempt>> ListAsync(string userId, string quizId, CancellationToken cancellationToken = default)
        => (await Session.Query<AssessmentAttempt, AssessmentAttemptIndex>(index => index.UserId == userId && index.QuizId == quizId, collection: CollectionName)
            .OrderBy(index => index.StartedUtc)
            .ThenBy(index => index.Id)
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Lists the attempts of a learner, newest first.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attempts.</returns>
    /// <summary>
    /// Lists the ids of a quiz's attempts. The documents are read rather than the index rows, whose decimal columns
    /// SQLite may hand back as integers on one row and reals on the next.
    /// </summary>
    /// <param name="quizId">The quiz.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attempt ids.</returns>
    public async Task<IReadOnlyList<string>> ListIdsByQuizAsync(string quizId, CancellationToken cancellationToken = default)
    {
        const int pageSize = 200;

        var ids = new List<string>();

        for (var skip = 0; ; skip += pageSize)
        {
            var page = (await Session.Query<AssessmentAttempt, AssessmentAttemptIndex>(index => index.QuizId == quizId, collection: CollectionName)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(pageSize)
                .ListAsync(cancellationToken)).ToArray();

            ids.AddRange(page.Select(attempt => attempt.ItemId));

            if (page.Length < pageSize)
            {
                return ids;
            }
        }
    }

    public async Task<IReadOnlyList<AssessmentAttempt>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => (await Session.Query<AssessmentAttempt, AssessmentAttemptIndex>(index => index.UserId == userId, collection: CollectionName)
            .OrderByDescending(index => index.StartedUtc)
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Reads a page of the attempts that wait for a grader, oldest submission first, optionally of some courses.
    /// </summary>
    /// <param name="courseIds">The courses, or <see langword="null"/> for every course.</param>
    /// <param name="page">The page, from one.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    public async Task<(int Count, IReadOnlyList<AssessmentAttempt> Items)> PageNeedingGradingAsync(IReadOnlyCollection<string> courseIds, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<AssessmentAttempt, AssessmentAttemptIndex>(index => index.NeedsGrading, collection: CollectionName);

        if (courseIds is not null)
        {
            var ids = courseIds.ToArray();
            query = query.Where(index => index.CourseId.IsIn(ids));
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(index => index.SubmittedUtc).ThenBy(index => index.Id).Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }

    /// <summary>
    /// Reads a page of attempts for an export or a data set, oldest first, optionally of one quiz and started within a
    /// window.
    /// </summary>
    /// <param name="quizId">The quiz, or <see langword="null"/>.</param>
    /// <param name="fromUtc">The earliest start, or <see langword="null"/>.</param>
    /// <param name="toUtc">The latest start, or <see langword="null"/>.</param>
    /// <param name="skip">The number to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    public async Task<IReadOnlyList<AssessmentAttempt>> PageForExportAsync(string quizId, DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<AssessmentAttempt, AssessmentAttemptIndex>(collection: CollectionName);

        if (!string.IsNullOrEmpty(quizId))
        {
            query = query.Where(index => index.QuizId == quizId);
        }

        if (fromUtc is { } from)
        {
            query = query.Where(index => index.StartedUtc >= from);
        }

        if (toUtc is { } to)
        {
            query = query.Where(index => index.StartedUtc <= to);
        }

        return (await query.OrderBy(index => index.StartedUtc).ThenBy(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Reads an attempt again from the store, leaving out any copy the session already holds, so a write under the
    /// attempt lock sees what the last writer committed.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attempt, or <see langword="null"/>.</returns>
    public async Task<AssessmentAttempt> FindFreshAsync(string attemptId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(attemptId))
        {
            return null;
        }

        var tracked = await FindByIdAsync(attemptId, cancellationToken);

        if (tracked is null)
        {
            return null;
        }

        Session.Detach(tracked, CollectionName);

        return await FindByIdAsync(attemptId, cancellationToken);
    }

    /// <summary>
    /// Lists the attempts in progress whose deadline passed, for the upkeep that catches any missed deadline.
    /// </summary>
    /// <param name="utcNow">The time now.</param>
    /// <param name="take">The most to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attempts.</returns>
    public async Task<IReadOnlyList<AssessmentAttempt>> ListOverdueAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default)
        => (await Session.Query<AssessmentAttempt, AssessmentAttemptIndex>(
                index => index.Status == AttemptStatus.InProgress && index.DeadlineUtc != null && index.DeadlineUtc <= utcNow,
                collection: CollectionName)
            .OrderBy(index => index.DeadlineUtc)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
}

/// <summary>
/// Stores overrides.
/// </summary>
public sealed class AssessmentOverrideStore : DocumentCatalog<AssessmentOverride, AssessmentOverrideIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentOverrideStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public AssessmentOverrideStore(ISession session)
        : base(session)
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <summary>
    /// Finds the newest override of a learner on a quiz.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="quizId">The quiz.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The override, or <see langword="null"/>.</returns>
    public async Task<AssessmentOverride> FindLatestAsync(string userId, string quizId, CancellationToken cancellationToken = default)
        => await Session.Query<AssessmentOverride, AssessmentOverrideIndex>(index => index.UserId == userId && index.QuizId == quizId, collection: CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Lists the overrides of a quiz, newest first.
    /// </summary>
    /// <param name="quizId">The quiz.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The overrides.</returns>
    public async Task<IReadOnlyList<AssessmentOverride>> ListByQuizAsync(string quizId, CancellationToken cancellationToken = default)
        => (await Session.Query<AssessmentOverride, AssessmentOverrideIndex>(index => index.QuizId == quizId, collection: CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Lists the overrides of a learner.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The overrides.</returns>
    public async Task<IReadOnlyList<AssessmentOverride>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => (await Session.Query<AssessmentOverride, AssessmentOverrideIndex>(index => index.UserId == userId, collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();
}

/// <summary>
/// Stores grade scales.
/// </summary>
public sealed class GradeScaleStore : DocumentCatalog<GradeScale, GradeScaleIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GradeScaleStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public GradeScaleStore(ISession session)
        : base(session)
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <summary>
    /// Finds a grade scale by name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scale, or <see langword="null"/>.</returns>
    public async Task<GradeScale> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(name)
            ? null
            : await Session.Query<GradeScale, GradeScaleIndex>(index => index.Name == name, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
}
