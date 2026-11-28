using System.Net;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Required learning and audiences on a running site: an audience administrators define resolves its members, a rule
/// run on it requires the course of them and shows on their My required learning page, and the admin pages are refused
/// to a learner.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class RequiredLearningPagesTests
{
    private readonly LearningSiteFixture _fixture;

    public RequiredLearningPagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ARuleOnAnAudience_RequiresTheCourseOfItsMembers_AndTheyFindItOnTheirPage()
    {
        // Arrange: an audience of the learners of the fixture course, and a rule that names the spare learner.
        var name = "required-" + Guid.NewGuid().ToString("n")[..6];

        // The audiences are read once first, as on a running site: the document is cached from then on, and an audience
        // saved later must still be found in the scope that saved it.
        await _fixture.InTenantAsync(services => services.GetRequiredService<IUserAudienceService>().ListAsync());

        var (members, run, state) = await _fixture.InTenantAsync(async services =>
        {
            var audiences = services.GetRequiredService<IUserAudienceService>();
            var errors = await audiences.SaveDefinitionAsync(new UserAudienceDefinition
            {
                Name = name,
                DisplayName = "Required learning test",
                Rules = [new UserAudienceRule { Source = "CourseLearner", Values = [_fixture.CourseId] }],
            });

            Assert.Empty(errors);

            var required = services.GetRequiredService<IRequiredLearningService>();
            var requirement = new LearningRequirement
            {
                Name = "Safety course for " + name,
                TargetKind = ProgramsConstants.TargetKinds.Course,
                TargetId = _fixture.OtherCourseId,
                UserIds = [_fixture.SpareLearnerId],
                DueRule = RequiredLearningDueRule.DaysAfterRequired,
                DueDays = 14,
            };

            Assert.Empty(await required.SaveAsync(requirement));

            var result = await required.RunAsync(requirement.ItemId, "test");
            var (_, items) = await required.PageAsync(requirement.ItemId, null, 0, 10);

            return ((await audiences.GetMembersAsync(name, 0, 50)).UserIds, result, items.Single());
        });

        using var learner = await _fixture.CreateClientAsync("learner3");
        var page = await learner.GetStringAsync("learning/required", TestContext.Current.CancellationToken);

        using var otherLearner = await _fixture.CreateClientAsync("learner1");
        var refused = await otherLearner.GetAsync("Admin/learning/required", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(_fixture.EnrolledUserId, members);
        Assert.Equal(1, run.Added);
        Assert.Equal(_fixture.SpareLearnerId, state.UserId);
        Assert.NotNull(state.DueUtc);
        Assert.Contains("My Required Learning", page);
        Assert.Contains("Due", page);
        // Refused, not challenged: a signed-in person is sent to the access denied page.
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.DoesNotContain("Login", refused.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MyRequiredLearning_NeedsASignIn()
    {
        // Arrange
        var anonymous = await _fixture.CreateClientAsync();

        // Act
        var page = await anonymous.GetAsync("learning/required", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Contains("Login", page.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
