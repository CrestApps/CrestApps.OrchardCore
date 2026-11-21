using System.Globalization;
using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Dapper;
using YesSql;
using YesSql.Services;
using YesSql.Sql;

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
    /// Lists the passed attempts at a quiz submitted since a time, oldest first.
    /// </summary>
    /// <param name="quizId">The quiz.</param>
    /// <param name="sinceUtc">The earliest submission time, in UTC.</param>
    /// <param name="take">The most attempts to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attempts.</returns>
    /// <summary>
    /// Lists everyone who passed a quiz since a time, reading the index a page at a time to the end.
    /// </summary>
    /// <param name="quizId">The quiz.</param>
    /// <param name="sinceUtc">The time, in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user ids, each once.</returns>
    public async Task<IReadOnlyCollection<string>> ListPassedUserIdsSinceAsync(string quizId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        const int pageSize = 1000;

        // Only the row id and the user id are read. The index's decimal columns (Percent, Score) come back from SQLite as
        // an integer on one row and a real on the next, and a typed row reader built from the first row throws on the
        // next one, so the whole index row is never mapped here.
        var configuration = Session.Store.Configuration;
        var dialect = configuration.SqlDialect;
        var id = dialect.QuoteForColumnName("Id");
        var builder = new SqlBuilder(configuration.TablePrefix, dialect);

        builder.Select();
        builder.Selector($"{id}, {dialect.QuoteForColumnName(nameof(AssessmentAttemptIndex.UserId))}");
        builder.Table(configuration.TableNameConvention.GetIndexTable(typeof(AssessmentAttemptIndex), CollectionName), null, configuration.Schema);
        builder.WhereAnd($"{dialect.QuoteForColumnName(nameof(AssessmentAttemptIndex.QuizId))} = @QuizId");
        builder.WhereAnd($"{dialect.QuoteForColumnName(nameof(AssessmentAttemptIndex.Passed))} = @Passed");
        builder.WhereAnd($"{dialect.QuoteForColumnName(nameof(AssessmentAttemptIndex.SubmittedUtc))} >= @SinceUtc");
        builder.WhereAnd($"{id} > @AfterId");
        builder.OrderBy(id);
        builder.Take(pageSize.ToString(CultureInfo.InvariantCulture));

        var sql = builder.ToSqlString();
        var users = new HashSet<string>(StringComparer.Ordinal);

        // The statement runs on the session's own transaction, so it first has to see what this unit of work wrote.
        await Session.FlushAsync(cancellationToken);
        var transaction = await Session.BeginTransactionAsync(cancellationToken);

        for (var afterId = 0L; ;)
        {
            var rows = (await transaction.Connection.QueryAsync<PassedAttemptRow>(new CommandDefinition(
                sql,
                new { QuizId = quizId, Passed = true, SinceUtc = sinceUtc, AfterId = afterId },
                transaction,
                cancellationToken: cancellationToken))).ToArray();

            users.UnionWith(rows.Select(row => row.UserId));

            if (rows.Length < pageSize)
            {
                return users;
            }

            afterId = rows[^1].Id;
        }
    }

    private sealed class PassedAttemptRow
    {
        public long Id { get; set; }

        public string UserId { get; set; }
    }

    public async Task<IReadOnlyList<AssessmentAttempt>> ListPassedSinceAsync(string quizId, DateTime sinceUtc, int take, CancellationToken cancellationToken = default)
    {
        return (await Session.Query<AssessmentAttempt, AssessmentAttemptIndex>(
            index => index.QuizId == quizId && index.Passed == true && index.SubmittedUtc >= sinceUtc,
            collection: CollectionName)
            .OrderBy(index => index.SubmittedUtc)
            .ThenBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
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
