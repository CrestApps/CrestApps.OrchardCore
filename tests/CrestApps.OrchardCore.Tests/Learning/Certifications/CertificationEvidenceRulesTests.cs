using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Certifications;

/// <summary>
/// Review round 4, finding 2: a completed enrollment counts as evidence only when it is not failed and has no open
/// review, and a passed attempt counts only when it was made in an enrollment that passes the same two checks. A
/// staff member's preview attempt, made in no enrollment, never counts.
/// </summary>
public sealed class CertificationEvidenceRulesTests
{
    [Fact]
    public async Task AFailedCompletion_EarnsNoCertificate()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        await h.AddPathAsync();
        await h.CompleteCourseAsync("ana", "safety");
        await ChangeEnrollmentAsync(h, "ana", "safety", enrollment => enrollment.Passed = false);

        // Act
        await h.PassQuizAsync("ana");

        // Assert
        Assert.Empty(await h.RecordsAsync("ana"));
    }

    [Fact]
    public async Task ACompletionUnderReview_EarnsNoCertificate_UntilTheReviewIsCleared()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        var path = await h.AddPathAsync();
        await h.CompleteCourseAsync("ana", "safety");
        await ChangeEnrollmentAsync(h, "ana", "safety", enrollment => enrollment.Review = new EnrollmentReview { Reason = LearningReviewReasons.ResultChangedAfterCompletion, FlaggedUtc = DateTime.UtcNow });

        // Act
        await h.PassQuizAsync("ana");
        var whileReviewed = await h.RecordsAsync("ana");

        await ChangeEnrollmentAsync(h, "ana", "safety", enrollment => enrollment.Review.ClearedUtc = DateTime.UtcNow);
        await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().EvaluateAsync(path.ItemId, "ana"));
        var afterClearing = await h.RecordsAsync("ana");

        // Assert
        Assert.Empty(whileReviewed);
        Assert.Single(afterClearing);
    }

    [Fact]
    public async Task APassedAttemptInAnEnrollmentUnderReview_OrInNoEnrollment_EarnsNoCertificate()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        var path = await h.AddPathAsync();
        await h.CompleteCourseAsync("ana", "safety");
        await h.CompleteCourseAsync("ben", "safety");

        // ana passes the exam in an enrollment that is then flagged; ben's only pass is a preview made in no enrollment.
        await h.PassQuizAsync("ana");
        await ChangeEnrollmentAsync(h, "ana", "exams", enrollment => enrollment.Review = new EnrollmentReview { Reason = LearningReviewReasons.ResultChangedAfterCompletion, FlaggedUtc = DateTime.UtcNow });

        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<AssessmentAttemptStore>();

            await store.CreateAsync(new AssessmentAttempt
            {
                UserId = "ben",
                QuizId = "final",
                CourseId = "exams",
                Status = AttemptStatus.Submitted,
                Passed = true,
                Percent = 100,
                StartedUtc = h.Clock.UtcNow,
                SubmittedUtc = h.Clock.UtcNow,
            });
        });

        // Act
        var ana = await EvidenceKindsAsync(h, path, "ana");
        var ben = await EvidenceKindsAsync(h, path, "ben");

        // Assert: the course counts for both, the exam for neither.
        Assert.Equal(["CourseCompleted"], ana);
        Assert.Equal(["CourseCompleted"], ben);
    }

    private static Task<string[]> EvidenceKindsAsync(CertificationHarness h, CertificationPath path, string userId)
        => h.RunAsync(async services =>
        {
            var kinds = new List<string>();

            foreach (var requirement in path.InitialRequirements)
            {
                var source = services.GetServices<ICertificationRequirement>().Single(item => item.Kind == requirement.Kind);
                var evidence = await source.FindEvidenceAsync(new CertificationRequirementContext { UserId = userId, Requirement = requirement });

                if (evidence is not null)
                {
                    kinds.Add(evidence.Kind);
                }
            }

            return kinds.ToArray();
        });

    private static Task ChangeEnrollmentAsync(CertificationHarness h, string userId, string courseId, Action<Enrollment> change)
        => h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var enrollment = (await store.ListByCourseForUserAsync(userId, courseId)).Single();

            change(enrollment);
            await store.SaveAsync(enrollment);
        });
}
