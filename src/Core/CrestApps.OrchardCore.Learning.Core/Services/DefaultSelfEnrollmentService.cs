using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Enrolls a signed-in learner as the course's enrollment mode allows. The grant's source id is the course, so asking
/// twice never adds a second grant.
/// </summary>
public sealed class DefaultSelfEnrollmentService : ISelfEnrollmentService
{
    private readonly IEnrollmentService _enrollmentService;
    private readonly ICourseStructureService _courseStructure;
    private readonly CourseEnrollmentKeyStore _keyStore;
    private readonly EnrollmentKeyAttempts _attempts;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSelfEnrollmentService"/> class.
    /// </summary>
    /// <param name="enrollmentService">The enrollment service.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="keyStore">The enrollment keys.</param>
    /// <param name="attempts">The limit on wrong keys.</param>
    /// <param name="clock">The clock.</param>
    public DefaultSelfEnrollmentService(
        IEnrollmentService enrollmentService,
        ICourseStructureService courseStructure,
        CourseEnrollmentKeyStore keyStore,
        EnrollmentKeyAttempts attempts,
        IClock clock)
    {
        _enrollmentService = enrollmentService;
        _courseStructure = courseStructure;
        _keyStore = keyStore;
        _attempts = attempts;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> EnrollAsync(string userId, string courseId, string enrollmentKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(courseId))
        {
            return EnrollmentResult.Fail(EnrollmentErrors.Invalid);
        }

        var course = await _courseStructure.GetCourseAsync(courseId);

        if (course is null)
        {
            return EnrollmentResult.Fail(EnrollmentErrors.CourseNotFound);
        }

        var part = course.Get<CoursePart>(nameof(CoursePart));
        var source = LearningConstants.EnrollmentSources.Self;
        var requiresApproval = false;

        switch (part.EnrollmentMode)
        {
            case EnrollmentMode.Open:
                break;

            case EnrollmentMode.Key:
                if (await _attempts.IsLockedOutAsync(userId, courseId, cancellationToken))
                {
                    return EnrollmentResult.Fail(EnrollmentErrors.TooManyAttempts);
                }

                if (!await _keyStore.VerifyAsync(courseId, enrollmentKey, cancellationToken))
                {
                    await _attempts.RecordFailureAsync(userId, courseId, cancellationToken);

                    return EnrollmentResult.Fail(EnrollmentErrors.InvalidKey);
                }

                await _attempts.ResetAsync(userId, courseId, cancellationToken);

                source = LearningConstants.EnrollmentSources.Key;
                break;

            case EnrollmentMode.Approval:
                requiresApproval = true;
                break;

            default:
                return EnrollmentResult.Fail(EnrollmentErrors.Closed);
        }

        return await _enrollmentService.EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = source,
            SourceId = courseId,
            AccessEndsUtc = part.AccessDays is > 0 ? _clock.UtcNow.AddDays(part.AccessDays.Value) : null,
            RequiresApproval = requiresApproval,
            Actor = userId,
        }, cancellationToken);
    }
}
