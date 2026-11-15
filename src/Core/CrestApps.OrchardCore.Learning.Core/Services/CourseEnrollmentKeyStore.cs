using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Keeps the enrollment key hash of each course, out of the course content item.
/// </summary>
public sealed class CourseEnrollmentKeyStore : DocumentCatalog<CourseEnrollmentKey, CourseEnrollmentKeyIndex>
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseEnrollmentKeyStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    public CourseEnrollmentKeyStore(ISession session, IClock clock)
        : base(session)
    {
        CollectionName = LearningConstants.CollectionName;
        _clock = clock;
    }

    /// <summary>
    /// Finds the key of a course.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public async Task<CourseEnrollmentKey> FindByCourseAsync(string courseId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(courseId)
            ? null
            : await Session.Query<CourseEnrollmentKey, CourseEnrollmentKeyIndex>(index => index.CourseId == courseId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Gets whether a course has a key.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    public async Task<bool> HasKeyAsync(string courseId, CancellationToken cancellationToken = default)
        => !string.IsNullOrEmpty((await FindByCourseAsync(courseId, cancellationToken))?.Hash);

    /// <summary>
    /// Sets the key of a course, keeping only its hash.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task SetAsync(string courseId, string key, CancellationToken cancellationToken = default)
        => SetHashAsync(courseId, EnrollmentKeys.Hash(key), cancellationToken);

    /// <summary>
    /// Sets the hash of the key of a course, such as one read from a recipe.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="hash">The hash.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task SetHashAsync(string courseId, string hash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(courseId);

        var key = await FindByCourseAsync(courseId, cancellationToken);

        if (string.IsNullOrEmpty(hash))
        {
            if (key is not null)
            {
                await DeleteAsync(key, cancellationToken);
            }

            return;
        }

        if (key is null)
        {
            await CreateAsync(new CourseEnrollmentKey { CourseId = courseId, Hash = hash, UpdatedUtc = _clock.UtcNow }, cancellationToken);

            return;
        }

        key.Hash = hash;
        key.UpdatedUtc = _clock.UtcNow;
        await UpdateAsync(key, cancellationToken);
    }

    /// <summary>
    /// Checks a key typed by a learner.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it is the key of the course.</returns>
    public async Task<bool> VerifyAsync(string courseId, string key, CancellationToken cancellationToken = default)
        => EnrollmentKeys.Verify(key, (await FindByCourseAsync(courseId, cancellationToken))?.Hash);

    /// <summary>
    /// Lists every key, for an export.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The keys.</returns>
    public async Task<IReadOnlyList<CourseEnrollmentKey>> ListAllAsync(CancellationToken cancellationToken = default)
        => (await Session.Query<CourseEnrollmentKey, CourseEnrollmentKeyIndex>(collection: CollectionName).ListAsync(cancellationToken)).ToArray();
}
