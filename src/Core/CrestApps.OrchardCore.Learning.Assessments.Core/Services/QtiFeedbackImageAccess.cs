using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Decides who may read a picture of imported QTI feedback: staff (View question banks, Grade assessments, or a grader,
/// instructor or assistant of a course with Manage the courses you teach), and a learner only once the review of an
/// attempt of theirs shows feedback that holds the picture (<see cref="AttemptReviewPolicy"/>).
/// </summary>
public sealed class QtiFeedbackImageAccess
{
    private readonly IAuthorizationService _authorizationService;
    private readonly GradingScope _gradingScope;
    private readonly AssessmentAttemptStore _attempts;
    private readonly AttemptReviewPolicy _reviewPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="QtiFeedbackImageAccess"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="gradingScope">The grading scope.</param>
    /// <param name="attempts">The attempt store.</param>
    /// <param name="reviewPolicy">The review policy.</param>
    public QtiFeedbackImageAccess(IAuthorizationService authorizationService, GradingScope gradingScope, AssessmentAttemptStore attempts, AttemptReviewPolicy reviewPolicy)
    {
        _authorizationService = authorizationService;
        _gradingScope = gradingScope;
        _attempts = attempts;
        _reviewPolicy = reviewPolicy;
    }

    /// <summary>
    /// Gets whether a person may read a picture of feedback.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="fileId">The picture's protected file id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public async Task<bool> CanReadAsync(ClaimsPrincipal user, string fileId, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(fileId))
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, AssessmentPermissions.ViewQuestionBanks))
        {
            return true;
        }

        // Graders see the feedback of the work they grade.
        var gradedCourses = await _gradingScope.GetCourseIdsAsync(user);

        if (gradedCourses is null || gradedCourses.Count > 0)
        {
            return true;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        // A learner: an attempt of theirs whose review is open and whose feedback holds the picture.
        var path = QtiFeedbackImages.PathOf(fileId);

        foreach (var attempt in await _attempts.ListByUserAsync(userId, cancellationToken))
        {
            if (_reviewPolicy.CanReview(attempt) && attempt.Items.Any(item => item.Feedback?.Contains(path, StringComparison.Ordinal) == true))
            {
                return true;
            }
        }

        return false;
    }
}
