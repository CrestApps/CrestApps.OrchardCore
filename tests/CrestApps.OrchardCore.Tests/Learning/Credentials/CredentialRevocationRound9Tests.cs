using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// Review round 9, H4: a credential is revoked when what it rests on goes away (the certificate is revoked, the quiz
/// attempt is voided or is no longer a pass), and an enrollment flagged for review offers the revocation of the
/// credential its completion awarded as a review action instead of revoking it by itself.
/// </summary>
public sealed class CredentialRevocationRound9Tests
{
    [Fact]
    public async Task Round9H4_ARevokedCertificate_RevokesTheCredentialItAwarded()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("cert-badge", new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.CertificationIssued, TargetId = "cert1" });
        var record = new CertificationRecord { ItemId = "record1", CertificationId = "cert1", Number = "C-1", SubjectKind = SkillSubjectKind.User, SubjectId = "ana", Status = CertificationStatus.Valid };

        await NotifyCertificateAsync(h, (observer, context) => observer.IssuedAsync(context), record, actor: null);
        await h.RunQueuedAsync();
        Assert.False(Assert.Single(await h.AwardsAsync("ana")).IsRevoked);

        // Act: the certificate is revoked for fraud.
        record.Status = CertificationStatus.Revoked;
        await NotifyCertificateAsync(h, (observer, context) => observer.RevokedAsync(context), record, actor: "auditor");

        // Assert
        var award = Assert.Single(await h.AwardsAsync("ana"));

        Assert.True(award.IsRevoked);
        Assert.Equal("auditor", award.RevokedBy);
        Assert.False(string.IsNullOrWhiteSpace(award.RevokedReason));
        Assert.Equal(CredentialAwardStatus.Revoked, award.GetStatus(h.Clock.UtcNow));
    }

    [Fact]
    public async Task Round9H4_AnAttemptNoLongerAPass_OrVoided_RevokesTheQuizCredential()
    {
        // Arrange: ana and ben both pass the exam and receive the badge.
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("exam-badge", new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.QuizPassed, TargetId = "final" });

        var ana = await h.PassQuizAsync("ana");
        var ben = await h.PassQuizAsync("ben");
        await h.RunQueuedAsync();

        // Act: ana's attempt is regraded below the pass mark; ben's is voided.
        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<AssessmentAttemptStore>();
            var regraded = await store.FindByIdAsync(ana.ItemId);
            var voided = await store.FindByIdAsync(ben.ItemId);

            regraded.Passed = false;
            voided.Status = AttemptStatus.Voided;

            foreach (var observer in services.GetServices<IAttemptObserver>())
            {
                await observer.ScoreChangedAsync(regraded, new AttemptScoreChange { ChangedUtc = h.Clock.UtcNow, Actor = "grader", Kind = "Regraded", From = 100, To = 0, Reason = "Key corrected" });
                await observer.ScoreChangedAsync(voided, new AttemptScoreChange { ChangedUtc = h.Clock.UtcNow, Actor = "grader", Kind = "Voided", From = 100, To = null, Reason = "Cheating" });
            }
        });

        // Assert
        Assert.True(Assert.Single(await h.AwardsAsync("ana")).IsRevoked);
        Assert.True(Assert.Single(await h.AwardsAsync("ben")).IsRevoked);
    }

    [Fact]
    public async Task Round9H4_AnEnrollmentFlaggedForReview_OffersTheRevocation_AndRevokesNothingByItself()
    {
        // Arrange: ana completes the course and receives the badge; her completion is then flagged for review.
        var links = new Mock<LinkGenerator>();

        links.Setup(generator => generator.GetPathByAddress(
                It.IsAny<HttpContext>(),
                It.IsAny<RouteValuesAddress>(),
                It.IsAny<RouteValueDictionary>(),
                It.IsAny<RouteValueDictionary>(),
                It.IsAny<PathString?>(),
                It.IsAny<FragmentString>(),
                It.IsAny<LinkOptions>()))
            .Returns("/admin/revoke-from-review");

        await using var h = await CredentialTestHarness.CreateAsync(services =>
        {
            // A fixed accessor: HttpContextAccessor keeps its context in an async local, which this setup would not flow.
            services.AddSingleton(Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == new DefaultHttpContext()));
            services.AddSingleton(links.Object);
        });
        h.Definitions.Add("course-badge", new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.CourseCompleted, TargetId = "safety" });

        var enrollment = await h.CompleteCourseAsync("ana");
        await h.RunQueuedAsync();

        // Act
        var actions = await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var flagged = await store.FindByIdAsync(enrollment.ItemId);

            flagged.Review = new EnrollmentReview { Reason = LearningReviewReasons.ResultChangedAfterCompletion, FlaggedUtc = h.Clock.UtcNow };
            await store.SaveAsync(flagged);

            foreach (var observer in services.GetServices<IEnrollmentObserver>())
            {
                await observer.ReviewNeededAsync(flagged);
            }

            var providers = typeof(CrestApps.OrchardCore.Learning.Credentials.Startup).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(IEnrollmentReviewActionProvider).IsAssignableFrom(type))
                .ToArray();
            var provider = (IEnrollmentReviewActionProvider)ActivatorUtilities.CreateInstance(services, Assert.Single(providers));

            return (await provider.GetActionsAsync(flagged)).ToArray();
        });

        // Assert: the award stands until staff decide, and the review card offers to revoke it.
        Assert.False(Assert.Single(await h.AwardsAsync("ana")).IsRevoked);

        var action = Assert.Single(actions);

        Assert.Equal("/admin/revoke-from-review", action.Url);
        Assert.False(string.IsNullOrEmpty(action.Confirmation));
    }

    private static Task NotifyCertificateAsync(CredentialTestHarness h, Func<ICertificationRecordObserver, CertificationRecordContext, Task> notify, CertificationRecord record, string actor)
        => h.RunAsync(async services =>
        {
            foreach (var observer in services.GetServices<ICertificationRecordObserver>())
            {
                await notify(observer, new CertificationRecordContext { Record = record, Actor = actor });
            }
        });
}
