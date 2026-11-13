using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Stores enrollments, one document each, in the <see cref="LearningConstants.CollectionName"/> collection. Its writes
/// skip the enrollment rules; change enrollments through <see cref="IEnrollmentService"/>.
/// </summary>
public sealed class EnrollmentStore : DocumentCatalog<Enrollment, EnrollmentIndex>, IEnrollmentStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public EnrollmentStore(ISession session)
        : base(session)
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <inheritdoc />
    async Task<Enrollment> IEnrollmentStore.FindByIdAsync(string enrollmentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(enrollmentId))
        {
            return null;
        }

        return await FindByIdAsync(enrollmentId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Enrollment> FindOpenAsync(string userId, string courseId, string offeringId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
        {
            return null;
        }

        var offering = string.IsNullOrEmpty(offeringId) ? null : offeringId;

        return await Session.Query<Enrollment, EnrollmentIndex>(
                index => index.UserId == userId && index.CourseId == courseId && index.IsOpen && index.OfferingId == offering,
                collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Enrollment>> ListByCourseForUserAsync(string userId, string courseId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
        {
            return [];
        }

        return (await Session.Query<Enrollment, EnrollmentIndex>(index => index.UserId == userId && index.CourseId == courseId, collection: CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.Id)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Enrollment>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        return (await Session.Query<Enrollment, EnrollmentIndex>(index => index.UserId == userId, collection: CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.Id)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Enrollment>> ListByGrantAsync(string source, string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(sourceId))
        {
            return [];
        }

        return (await Session.Query<Enrollment, EnrollmentGrantIndex>(index => index.Source == source && index.SourceId == sourceId, collection: CollectionName)
            .ListAsync(cancellationToken)).DistinctBy(enrollment => enrollment.ItemId).ToArray();
    }

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<Enrollment> Items)> PageAsync(EnrollmentQuery query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        query ??= new EnrollmentQuery();

        if (query.CourseIds is { Count: 0 } || query.UserIds is { Count: 0 })
        {
            return (0, []);
        }

        var search = Session.Query<Enrollment, EnrollmentIndex>(collection: CollectionName);

        if (!string.IsNullOrEmpty(query.CourseId))
        {
            search = search.Where(index => index.CourseId == query.CourseId);
        }

        if (query.CourseIds is not null)
        {
            search = search.Where(index => index.CourseId.IsIn(query.CourseIds));
        }

        if (!string.IsNullOrEmpty(query.UserId))
        {
            search = search.Where(index => index.UserId == query.UserId);
        }

        if (query.UserIds is not null)
        {
            search = search.Where(index => index.UserId.IsIn(query.UserIds));
        }

        if (query.Status is { } status)
        {
            search = search.Where(index => index.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Number))
        {
            var number = query.Number.Trim();

            search = search.Where(index => index.Number == number);
        }

        var count = await search.CountAsync(cancellationToken);
        var items = await search
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.Id)
            .Skip(Math.Max(0, page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync(cancellationToken);

        return (count, items.ToArray());
    }

    /// <summary>
    /// Lists open enrollments whose access ended before a time, in pages.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollments.</returns>
    public async Task<IReadOnlyList<Enrollment>> ListEndedAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default)
    {
        return (await Session.Query<Enrollment, EnrollmentIndex>(index => index.IsOpen && index.AccessEndsUtc != null && index.AccessEndsUtc <= utcNow, collection: CollectionName)
            .OrderBy(index => index.AccessEndsUtc)
            .ThenBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Lists active enrollments whose access ends within a window, soonest first.
    /// </summary>
    /// <param name="fromUtc">The start of the window, in UTC.</param>
    /// <param name="toUtc">The end of the window, in UTC.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollments.</returns>
    public async Task<IReadOnlyList<Enrollment>> ListAccessEndingAsync(DateTime fromUtc, DateTime toUtc, int take, CancellationToken cancellationToken = default)
    {
        return (await Session.Query<Enrollment, EnrollmentIndex>(
                index => index.IsOpen && index.Status == EnrollmentStatus.Active && index.AccessEndsUtc != null && index.AccessEndsUtc > fromUtc && index.AccessEndsUtc <= toUtc,
                collection: CollectionName)
            .OrderBy(index => index.AccessEndsUtc)
            .ThenBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Lists active enrollments whose learner has not worked on the course since a time, longest idle first.
    /// </summary>
    /// <param name="beforeUtc">The time, in UTC.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollments.</returns>
    public async Task<IReadOnlyList<Enrollment>> ListInactiveAsync(DateTime beforeUtc, int take, CancellationToken cancellationToken = default)
    {
        return (await Session.Query<Enrollment, EnrollmentIndex>(
                index => index.IsOpen && index.Status == EnrollmentStatus.Active
                    && ((index.LastActivityUtc != null && index.LastActivityUtc < beforeUtc) || (index.LastActivityUtc == null && index.CreatedUtc < beforeUtc)),
                collection: CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ThenBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Reads a page of enrollments for an export, oldest first, optionally of one course and created within a window.
    /// </summary>
    /// <param name="courseId">The course, or <see langword="null"/> for every course.</param>
    /// <param name="fromUtc">The earliest creation time, in UTC, or <see langword="null"/>.</param>
    /// <param name="toUtc">The latest creation time, in UTC, or <see langword="null"/>.</param>
    /// <param name="skip">The number of enrollments to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    public async Task<IReadOnlyList<Enrollment>> PageForExportAsync(string courseId, DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<Enrollment, EnrollmentIndex>(collection: CollectionName);

        if (!string.IsNullOrEmpty(courseId))
        {
            query = query.Where(index => index.CourseId == courseId);
        }

        if (fromUtc is { } from)
        {
            query = query.Where(index => index.CreatedUtc >= from);
        }

        if (toUtc is { } to)
        {
            query = query.Where(index => index.CreatedUtc <= to);
        }

        return (await query.OrderBy(index => index.CreatedUtc).ThenBy(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Lists active enrollments due within a window, soonest first.
    /// </summary>
    /// <param name="fromUtc">The start of the window, in UTC, excluded.</param>
    /// <param name="toUtc">The end of the window, in UTC, included.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollments.</returns>
    public async Task<IReadOnlyList<Enrollment>> ListDueAsync(DateTime fromUtc, DateTime toUtc, int take, CancellationToken cancellationToken = default)
    {
        return (await Session.Query<Enrollment, EnrollmentIndex>(
                index => index.IsOpen && index.Status == EnrollmentStatus.Active && index.DueUtc != null && index.DueUtc > fromUtc && index.DueUtc <= toUtc,
                collection: CollectionName)
            .OrderBy(index => index.DueUtc)
            .ThenBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Saves an enrollment.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task SaveAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Session.SaveAsync(enrollment, false, CollectionName, cancellationToken);
}
