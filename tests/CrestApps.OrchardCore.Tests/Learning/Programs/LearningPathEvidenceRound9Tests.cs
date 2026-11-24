using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.Rules;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Ledgers;
using CrestApps.OrchardCore.Tests.Learning.Credentials;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Programs;

/// <summary>
/// Review round 9, H3: a learning path completed through a course whose completion is no evidence (failed, under
/// review, or resting on a result the browser reported) awards neither the path's digital credential nor its education
/// credits, and so cannot meet a certification's credits requirement either.
/// </summary>
public sealed class LearningPathEvidenceRound9Tests
{
    [Theory]
    [InlineData("failed", false)]
    [InlineData("review", false)]
    [InlineData("browser", false)]
    [InlineData("clean", true)]
    public async Task Round9H3_APathCompletedThroughACourseThatIsNoEvidence_AwardsNoCredentialAndNoCredits(string flaw, bool awards)
    {
        // Arrange: a path of two courses with a credential rule and credits; c1 is completed first and then made
        // into what the case names, and completing c2 completes the path.
        var queue = new RecordingCredentialAwardQueue();
        var definitions = new InMemoryCredentialDefinitions();
        var credited = new List<EducationCreditAwardRequest>();
        var credits = new Mock<IEducationCreditService>();

        definitions.Add("path-badge", new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.LearningPathCompleted, TargetId = "p1" });
        credits.Setup(service => service.AwardAsync(It.IsAny<EducationCreditAwardRequest>(), It.IsAny<CancellationToken>()))
            .Callback((EducationCreditAwardRequest request, CancellationToken _) => credited.Add(request))
            .ReturnsAsync((QuantityLedgerPostResult)null);

        var h = await ProgramsHarness.CreateAsync(services =>
        {
            services.AddSingleton<ICredentialDefinitionCatalog>(definitions);
            services.AddSingleton<ICredentialAwardQueue>(queue);
            services.AddScoped<CredentialRuleTrigger>();
            services.AddLearningPathObserver<CredentialLearningPathObserver>();
            services.AddSingleton(credits.Object);
            services.AddLearningPathObserver<LearningPathCreditsObserver>();
        });

        h.AddPath("p1", new LearningPathCourseSet { Id = "s1", CourseIds = ["c1", "c2"] });
        h.Path("p1").Apply(new EducationCreditsPart { Awards = [new EducationCreditAward { CreditTypeId = "cpd", Amount = 2 }] });

        // Act
        await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest { PathId = "p1", UserId = "ana", Actor = "admin" }));
        await h.CompleteCourseAsync("ana", "c1");
        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var enrollment = (await store.ListByCourseForUserAsync("ana", "c1")).Single();

            switch (flaw)
            {
                case "failed":
                    enrollment.Passed = false;
                    break;
                case "review":
                    enrollment.Review = new EnrollmentReview { Reason = LearningReviewReasons.ResultChangedAfterCompletion, FlaggedUtc = h.Clock.UtcNow };
                    break;
                case "browser":
                    enrollment.Completion ??= new CompletionSnapshot();
                    enrollment.Completion.Items.Add(new CompletionSnapshotItem
                    {
                        ContentItemId = "package",
                        Title = "Package",
                        Required = true,
                        State = CourseItemState.Completed,
                        SelfReported = true,
                        ResultSelfReported = true,
                        AllowSelfReported = false,
                    });
                    break;
            }

            await store.SaveAsync(enrollment);
        });
        await h.CompleteCourseAsync("ana", "c2");

        var progress = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().FindProgressAsync("p1", "ana"));

        // Assert: the path itself is completed in every case; only what is issued on it depends on the evidence.
        Assert.Equal(LearningPathState.Completed, progress.State);

        if (awards)
        {
            Assert.Equal("path-badge", Assert.Single(queue.Requests).DefinitionId);
            Assert.Equal(2, Assert.Single(credited).Amount);
        }
        else
        {
            Assert.Empty(queue.Requests);
            Assert.Empty(credited);
        }
    }
}
