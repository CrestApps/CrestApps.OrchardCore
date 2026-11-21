using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Certifications;

/// <summary>
/// Review round 4, finding 1: a certificate can be revoked from the review of an enrollment only when the enrollment
/// has an open review and the certificate rests on it. A person who may manage one enrollment cannot revoke a
/// certificate that rests on another course, nor one recorded by hand.
/// </summary>
public sealed class CertificationReviewRevocationTests
{
    [Fact]
    public async Task OnlyACertificateRestingOnTheReviewedEnrollment_CanBeRevokedFromItsReview()
    {
        // Arrange: ana is certified by the safety course and the final exam.
        await using var h = await CertificationHarness.CreateAsync();
        h.Inner.Courses.AddCourse("other");
        h.Inner.Courses.AddItem("other", "intro");

        var path = await h.AddPathAsync();

        await h.CompleteCourseAsync("ana", "safety");
        await h.PassQuizAsync("ana");

        var record = Assert.Single(await h.RecordsAsync("ana"));
        var safety = Assert.Single(await h.EnrollmentsAsync("ana", "safety"));
        var other = await h.Inner.EnrollAsync("ana", "other");

        var handRecorded = await h.RunAsync(async services =>
        {
            var manual = new CertificationRecord { SubjectKind = SkillSubject.User("ana").Kind, SubjectId = "ana", CertificationId = record.CertificationId, IssuedOn = CertificationHarness.Today };

            await services.GetRequiredService<ICertificationRecordService>().SaveAsync(manual, "admin");

            return manual;
        });

        // Act
        var beforeReview = await CanRevokeAsync(h, safety.ItemId, record.ItemId);

        await FlagAsync(h, safety.ItemId);
        await FlagAsync(h, other.ItemId);

        var restingOnIt = await CanRevokeAsync(h, safety.ItemId, record.ItemId);
        var restingOnAnotherCourse = await CanRevokeAsync(h, other.ItemId, record.ItemId);
        var recordedByHand = await CanRevokeAsync(h, safety.ItemId, handRecorded.ItemId);

        // Assert
        Assert.False(beforeReview);
        Assert.True(restingOnIt);
        Assert.False(restingOnAnotherCourse);
        Assert.False(recordedByHand);
        Assert.NotNull(path);
    }

    private static Task<bool> CanRevokeAsync(CertificationHarness h, string enrollmentId, string recordId)
        => h.RunAsync(async services =>
        {
            var enrollment = await services.GetRequiredService<EnrollmentStore>().FindByIdAsync(enrollmentId);
            var responses = services.GetRequiredService<ISurveyResponseQueries>();

            return await services.GetRequiredService<DefaultCertificationPathService>().CanRevokeFromReviewAsync(
                enrollment,
                recordId,
                async responseId => LearningSurveyContexts.EnrollmentIdOf(await responses.FindByIdAsync(responseId)));
        });

    private static Task FlagAsync(CertificationHarness h, string enrollmentId)
        => h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var enrollment = await store.FindByIdAsync(enrollmentId);

            enrollment.Review = new EnrollmentReview { Reason = LearningReviewReasons.ResultChangedAfterCompletion, FlaggedUtc = DateTime.UtcNow };
            await store.SaveAsync(enrollment);
        });
}
