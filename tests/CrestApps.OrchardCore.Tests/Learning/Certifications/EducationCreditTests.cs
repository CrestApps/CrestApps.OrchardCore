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
        var accepted = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AcceptExternalAsync(submission.ItemId, "reviewer", "Looks good"));
        var stored = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().FindExternalAsync(submission.ItemId));
        var acceptedAgain = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AcceptExternalAsync(submission.ItemId, "reviewer", null));
        var entries = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().ListEntriesAsync("cid"));

        // Assert
        Assert.Equal(2, refused.Count);
        Assert.Empty(errors);
        Assert.Empty(waiting);
        Assert.Equal(ExternalLearningState.Accepted, accepted.State);
        Assert.Equal(ExternalLearningState.Accepted, stored.State);
        Assert.Equal("Looks good", stored.ReviewNote);
        Assert.Null(acceptedAgain);
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
        var (unknownUser, unknownType, badAmount, recorded) = await h.RunAsync(async services =>
        {
            var credits = services.GetRequiredService<IEducationCreditService>();

            return (
                await RecordExternalLearningStep.RecordRowAsync(credits, known, cpd, "zoe", "Course", null, 1m, day, "pipeline", TestContext.Current.CancellationToken),
                await RecordExternalLearningStep.RecordRowAsync(credits, known, null, "dee", "Course", null, 1m, day, "pipeline", TestContext.Current.CancellationToken),
                await RecordExternalLearningStep.RecordRowAsync(credits, known, cpd, "dee", "Course", null, 0m, day, "pipeline", TestContext.Current.CancellationToken),
                await RecordExternalLearningStep.RecordRowAsync(credits, known, cpd, "dee", "First aid", "Red Cross", 4m, day, "pipeline", TestContext.Current.CancellationToken));
        });
        var entries = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().ListEntriesAsync("dee"));
        var external = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().ListExternalForUserAsync("dee"));

        // Assert
        Assert.NotNull(unknownUser);
        Assert.NotNull(unknownType);
        Assert.NotNull(badAmount);
        Assert.Null(recorded);
        Assert.Equal(4m, Assert.Single(entries).Amount);
        Assert.Equal(ExternalLearningState.Accepted, Assert.Single(external).State);
    }
}
