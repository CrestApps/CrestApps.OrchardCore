using CrestApps.OrchardCore.Learning.Certifications;
using CrestApps.OrchardCore.Learning.Certifications.Core.Requirements;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Pipelines;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Certifications;

/// <summary>
/// Education credits: a completed course awards its credits once, credits earned in the cycle meet a credits
/// requirement and issue the certificate, credits from before the cycle do not count, and external learning posts its
/// credits only when it is accepted, once.
/// </summary>
public sealed class EducationCreditTests
{
    [Fact]
    public async Task CompletingACourse_AwardsItsCreditsOnce_AndCreditsInTheCycleIssueTheCertificate()
    {
        // Arrange
        CertificationHarness h = null;
        h = await CertificationHarness.CreateAsync(configure: services =>
        {
            var content = new Mock<IContentManager>();

            content.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync((string id, VersionOptions _) => id == "safety" ? h.Inner.Courses.Get("safety") : null);
            services.RemoveAll<IContentManager>();
            services.AddSingleton(content.Object);
        });

        var cpd = new EducationCreditType { Name = "CPD hours", Unit = "hours" };

        Assert.Empty(await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SaveTypeAsync(cpd)));
        h.Inner.Courses.Get("safety").Alter<EducationCreditsPart>(part => part.Awards = [new EducationCreditAward { CreditTypeId = cpd.ItemId, Amount = 2 }]);

        var path = await h.AddPathAsync(configure: path =>
        {
            path.InitialRequirements = [new CertificationRequirement { Kind = CertificationsConstants.RequirementKinds.CreditsEarned, TargetId = cpd.ItemId, MinimumGrade = 3 }];
        });

        await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().StartAsync(path.ItemId, "ana", "admin"));

        // Act: the course awards 2 hours; a second award for the same enrollment adds nothing.
        await h.CompleteCourseAsync("ana", "safety");

        var enrollment = (await h.EnrollmentsAsync("ana", "safety")).Single();
        var again = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AwardAsync(new EducationCreditAwardRequest
        {
            UserId = "ana",
            CreditTypeId = cpd.ItemId,
            Amount = 2,
            EarnedOn = CertificationHarness.Today,
            Source = CertificationsConstants.CreditSources.Course,
            SourceId = enrollment.ItemId,
        }));
        var before = await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().FindProgressAsync(path.ItemId, "ana"));

        // One more hour by hand meets the 3 hours.
        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AwardAsync(new EducationCreditAwardRequest
        {
            UserId = "ana",
            CreditTypeId = cpd.ItemId,
            Amount = 1,
            EarnedOn = CertificationHarness.Today,
            Source = CertificationsConstants.CreditSources.Manual,
            SourceId = "workshop-1",
        }));

        var after = await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().FindProgressAsync(path.ItemId, "ana"));
        var balances = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().GetBalancesAsync("ana", CertificationHarness.Today));

        // Assert
        Assert.False(again.Created);
        Assert.Equal(CertificationPathState.InProgress, before.State);
        Assert.Equal(CertificationPathState.Certified, after.State);
        Assert.NotNull(after.CurrentRecordId);
        Assert.Equal(3, balances[cpd.ItemId]);
    }

    [Fact]
    public async Task CreditsEarnedRequirement_CountsOnlyCreditsInTheCycle()
    {
        // Arrange
        var h = await CertificationHarness.CreateAsync();
        var pdu = new EducationCreditType { Name = "PDU" };

        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SaveTypeAsync(pdu));
        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AwardAsync(new EducationCreditAwardRequest
        {
            UserId = "ben",
            CreditTypeId = pdu.ItemId,
            Amount = 5,
            EarnedOn = CertificationHarness.Today.AddDays(-30),
            Source = CertificationsConstants.CreditSources.Manual,
            SourceId = "old",
        }));

        var requirement = new CertificationRequirement { Id = "r1", Kind = CertificationsConstants.RequirementKinds.CreditsEarned, TargetId = pdu.ItemId, MinimumGrade = 4 };

        // Act
        var sinceLastWeek = await h.RunAsync(services => services.GetServices<ICertificationRequirement>().OfType<CreditsEarnedRequirement>().Single()
            .FindEvidenceAsync(new CertificationRequirementContext { UserId = "ben", Requirement = requirement, SinceUtc = h.Clock.UtcNow.AddDays(-7) }));
        var always = await h.RunAsync(services => services.GetServices<ICertificationRequirement>().OfType<CreditsEarnedRequirement>().Single()
            .FindEvidenceAsync(new CertificationRequirementContext { UserId = "ben", Requirement = requirement }));
        var users = await h.RunAsync(services => services.GetServices<ICertificationRequirement>().OfType<CreditsEarnedRequirement>().Single()
            .ListUsersSinceAsync(requirement, h.Clock.UtcNow.AddMinutes(-5)));

        // Assert
        Assert.Null(sinceLastWeek);
        Assert.NotNull(always);
        Assert.Equal(5, always.Grade);
        Assert.Equal(["ben"], users);
    }

    [Fact]
    public async Task ExternalLearning_PostsCreditsOnlyWhenAccepted_Once()
    {
        // Arrange
        var h = await CertificationHarness.CreateAsync();
        var ceu = new EducationCreditType { Name = "CEU" };

        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SaveTypeAsync(ceu));

        var refused = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SubmitExternalAsync(new ExternalLearningSubmission { UserId = "cid", Title = "Conference", Amount = 0 }));
        var submission = new ExternalLearningSubmission { UserId = "cid", Title = "Safety conference", Provider = "NSC", CompletedOn = CertificationHarness.Today.AddDays(-2), CreditTypeId = ceu.ItemId, Amount = 1.5m };

        // Act
        var errors = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SubmitExternalAsync(submission));
        var waiting = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().GetBalancesAsync("cid", CertificationHarness.Today));
        var accepted = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AcceptExternalAsync(submission.ItemId, "reviewer-id", "reviewer", "Looks good"));
        var stored = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().FindExternalAsync(submission.ItemId));
        var acceptedAgain = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AcceptExternalAsync(submission.ItemId, "reviewer-id", "reviewer", null));
        var entries = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().ListEntriesAsync("cid"));

        // Assert
        Assert.Equal(2, refused.Count);
        Assert.Empty(errors);
        Assert.Empty(waiting);
        Assert.Equal(ExternalLearningState.Accepted, accepted.Submission.State);
        Assert.Equal(ExternalLearningState.Accepted, stored.State);
        Assert.Equal("Looks good", stored.ReviewNote);
        Assert.Equal(ExternalLearningReviewOutcome.AlreadyReviewed, acceptedAgain.Outcome);
        var entry = Assert.Single(entries);
        Assert.Equal(1.5m, entry.Amount);
        Assert.Equal(CertificationHarness.Today.AddDays(-2), entry.EffectiveDate);
        Assert.Equal("Safety conference", entry.Note);
    }

    [Fact]
    public async Task RecordExternalLearningRow_RejectsUnknownPeopleTypesAndAmounts_AndRecordsAcceptedCredits()
    {
        // Arrange
        var h = await CertificationHarness.CreateAsync();
        var cpd = new EducationCreditType { Name = "CPD hours" };

        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SaveTypeAsync(cpd));

        var known = new HashSet<string>(["dee"], StringComparer.Ordinal);
        var day = new DateTime(2026, 10, 20);

        // Act
        var S = new PassThroughLocalizer();
        var (unknownUser, unknownType, badAmount, own, recorded, again) = await h.RunAsync(async services =>
        {
            var credits = services.GetRequiredService<IEducationCreditService>();

            return (
                await RecordExternalLearningStep.RecordRowAsync(credits, S, known, cpd, "zoe", "Course", null, 1m, day, "runner", "pipeline", TestContext.Current.CancellationToken),
                await RecordExternalLearningStep.RecordRowAsync(credits, S, known, null, "dee", "Course", null, 1m, day, "runner", "pipeline", TestContext.Current.CancellationToken),
                await RecordExternalLearningStep.RecordRowAsync(credits, S, known, cpd, "dee", "Course", null, 0m, day, "runner", "pipeline", TestContext.Current.CancellationToken),
                await RecordExternalLearningStep.RecordRowAsync(credits, S, known, cpd, "dee", "First aid", "Red Cross", 4m, day, "dee", "pipeline", TestContext.Current.CancellationToken),
                await RecordExternalLearningStep.RecordRowAsync(credits, S, known, cpd, "dee", "First aid", "Red Cross", 4m, day, "runner", "pipeline", TestContext.Current.CancellationToken),
                await RecordExternalLearningStep.RecordRowAsync(credits, S, known, cpd, "dee", "First aid", "Red Cross", 4m, day, "runner", "pipeline", TestContext.Current.CancellationToken));
        });
        var entries = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().ListEntriesAsync("dee"));
        var external = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().ListExternalForUserAsync("dee"));

        // Assert
        Assert.NotNull(unknownUser.Error);
        Assert.NotNull(unknownType.Error);
        Assert.NotNull(badAmount.Error);
        Assert.NotNull(own.Error);
        Assert.True(recorded.Recorded);
        Assert.Null(recorded.Error);

        // Review round 5, finding 5: running the pipeline again posts nothing twice.
        Assert.False(again.Recorded);
        Assert.Null(again.Error);
        Assert.Equal(4m, Assert.Single(entries).Amount);
        Assert.Equal(ExternalLearningState.Accepted, Assert.Single(external).State);
    }

    [Fact]
    public async Task NobodyReviewsTheirOwnExternalLearning_AndARejectionNeverFollowsPostedCredits()
    {
        // Arrange: review round 5, findings 2 and 6.
        var h = await CertificationHarness.CreateAsync();
        var ceu = new EducationCreditType { Name = "CEU" };

        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SaveTypeAsync(ceu));

        var submission = new ExternalLearningSubmission { UserId = "rev", Title = "Forty hours", CompletedOn = CertificationHarness.Today, CreditTypeId = ceu.ItemId, Amount = 40 };

        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SubmitExternalAsync(submission));

        // Act
        var ownAccept = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AcceptExternalAsync(submission.ItemId, "rev", "rev", null));
        var ownReject = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().RejectExternalAsync(submission.ItemId, "rev", "rev", null));
        var balanceAfterOwn = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().GetBalancesAsync("rev", CertificationHarness.Today));
        var accepted = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AcceptExternalAsync(submission.ItemId, "other", "other", null));
        var rejectAfter = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().RejectExternalAsync(submission.ItemId, "third", "third", null));
        var tooMany = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SubmitExternalAsync(new ExternalLearningSubmission { UserId = "rev", Title = "Too many", CompletedOn = CertificationHarness.Today, CreditTypeId = ceu.ItemId, Amount = 100000 }));

        // Assert
        Assert.Equal(ExternalLearningReviewOutcome.OwnLearning, ownAccept.Outcome);
        Assert.Equal(ExternalLearningReviewOutcome.OwnLearning, ownReject.Outcome);
        Assert.Empty(balanceAfterOwn);
        Assert.True(accepted.Succeeded);
        Assert.Equal(ExternalLearningReviewOutcome.AlreadyReviewed, rejectAfter.Outcome);
        Assert.Single(tooMany);
    }

    [Fact]
    public async Task OneSourceId_AwardsEachPersonOnce_AndADeletedTypeReceivesNothing()
    {
        // Arrange: review round 5, finding 4 and the low item on deleted types.
        var h = await CertificationHarness.CreateAsync();
        var cpd = new EducationCreditType { Name = "CPD hours" };

        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SaveTypeAsync(cpd));

        EducationCreditAwardRequest Request(string userId, string typeId) => new()
        {
            UserId = userId,
            CreditTypeId = typeId,
            Amount = 1,
            EarnedOn = CertificationHarness.Today,
            Source = CertificationsConstants.CreditSources.Manual,
            SourceId = "webinar-1",
        };

        // Act
        var ana = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AwardAsync(Request("ana", cpd.ItemId)));
        var ben = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AwardAsync(Request("ben", cpd.ItemId)));
        var anaAgain = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AwardAsync(Request("ana", cpd.ItemId)));
        var deleted = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AwardAsync(Request("ana", "no-such-type")));

        // Assert
        Assert.True(ana.Created);
        Assert.True(ben.Created);
        Assert.False(anaAgain.Created);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task ExpiredCredits_NoLongerCertify_TheEvidenceNamesItsEnrollments_AndAReversalTakesThemBack()
    {
        // Arrange: review round 5, finding 13.
        var h = await CertificationHarness.CreateAsync();
        var pdu = new EducationCreditType { Name = "PDU" };

        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SaveTypeAsync(pdu));

        var requirement = new CertificationRequirement { Id = "r1", Kind = CertificationsConstants.RequirementKinds.CreditsEarned, TargetId = pdu.ItemId, MinimumGrade = 4 };

        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AwardAsync(new EducationCreditAwardRequest
        {
            UserId = "eve",
            CreditTypeId = pdu.ItemId,
            Amount = 10,
            EarnedOn = CertificationHarness.Today.AddYears(-3),
            ExpiresOn = CertificationHarness.Today.AddYears(-1),
            Source = CertificationsConstants.CreditSources.Manual,
            SourceId = "old-conference",
        }));

        Task<CertificationEvidence> EvidenceAsync() => h.RunAsync(services => services.GetServices<ICertificationRequirement>().OfType<CreditsEarnedRequirement>().Single()
            .FindEvidenceAsync(new CertificationRequirementContext { UserId = "eve", Requirement = requirement }));

        // Act
        var onlyExpired = await EvidenceAsync();

        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AwardAsync(new EducationCreditAwardRequest
        {
            UserId = "eve",
            CreditTypeId = pdu.ItemId,
            Amount = 5,
            EarnedOn = CertificationHarness.Today,
            Source = CertificationsConstants.CreditSources.Course,
            SourceId = "enrollment-1",
        }));
        var withCourse = await EvidenceAsync();
        var reversed = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().ReverseAsync("eve", "enrollment-1", "admin", "Exam voided"));
        var reversedAgain = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().ReverseAsync("eve", "enrollment-1", "admin", "Exam voided"));
        var afterReversal = await EvidenceAsync();

        // Assert
        Assert.Null(onlyExpired);
        Assert.NotNull(withCourse);
        Assert.Equal(5, withCourse.Grade);
        Assert.Equal(["enrollment-1"], withCourse.SourceRefs);
        Assert.Equal(1, reversed);
        Assert.Equal(0, reversedAgain);
        Assert.Null(afterReversal);
    }

    private sealed class PassThroughLocalizer : Microsoft.Extensions.Localization.IStringLocalizer
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name] => new(name, name);

        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
