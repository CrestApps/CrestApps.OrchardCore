using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Reads enrollments. Writes go through <see cref="IEnrollmentService"/>, so the rules and observers run.
/// </summary>
public interface IEnrollmentStore
{
    /// <summary>
    /// Finds an enrollment by id.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollment, or <see langword="null"/>.</returns>
    Task<Enrollment> FindByIdAsync(string enrollmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a learner's open enrollment in a course and offering.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="courseId">The course.</param>
    /// <param name="offeringId">The offering, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollment, or <see langword="null"/>.</returns>
    Task<Enrollment> FindOpenAsync(string userId, string courseId, string offeringId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a learner's enrollments in a course, newest first.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="courseId">The course.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollments.</returns>
    Task<IReadOnlyList<Enrollment>> ListByCourseForUserAsync(string userId, string courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a learner's enrollments, newest first.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollments.</returns>
    Task<IReadOnlyList<Enrollment>> ListByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the enrollments that hold a grant of a source record.
    /// </summary>
    /// <param name="source">The enrollment source.</param>
    /// <param name="sourceId">The id of the record in its source.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollments.</returns>
    Task<IReadOnlyList<Enrollment>> ListByGrantAsync(string source, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages enrollments for an admin list.
    /// </summary>
    /// <param name="query">The filters.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The total count and the page.</returns>
    Task<(int Count, IReadOnlyList<Enrollment> Items)> PageAsync(EnrollmentQuery query, int page, int pageSize, CancellationToken cancellationToken = default);
}
