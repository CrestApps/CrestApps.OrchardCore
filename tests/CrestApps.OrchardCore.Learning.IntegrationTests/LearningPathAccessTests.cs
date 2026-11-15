using System.Net;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Programs.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Review round 5, finding 7: a learning path respects the enrollment controls of its courses. Someone who may edit a
/// path but not manage the enrollments of its courses can neither list who is on it nor start people on it, and a path
/// that holds a course one cannot enroll in oneself offers no Start button.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningPathAccessTests
{
    private readonly LearningSiteFixture _fixture;

    public LearningPathAccessTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task APathWithAClosedCourse_OffersNoSelfStart_AndItsPeopleNeedManageEnrollments()
    {
        // Arrange: a path that allows self start and holds the closed "other" course; the instructor of the fixture
        // course may manage only that course's enrollments.
        var pathId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var path = await contentManager.NewAsync(ProgramsConstants.LearningPathContentType);

            path.DisplayText = "Closed path " + Guid.NewGuid().ToString("n")[..6];
            path.Alter<TitlePart>(part => part.Title = path.DisplayText);
            path.Alter<LearningPathPart>(part =>
            {
                part.AllowSelfStart = true;
                part.Sets = [new LearningPathCourseSet { Id = "s1", CourseIds = [_fixture.OtherCourseId] }];
            });
            await contentManager.CreateAsync(path, VersionOptions.Published);

            return path.ContentItemId;
        });

        var editorName = "patheditor" + Guid.NewGuid().ToString("n")[..6];

        await _fixture.InTenantAsync(services => LearningSiteFixture.RunRecipeAsync(services, new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = "PathEditorRole",
            ["steps"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = "Roles",
                ["Roles"] = new System.Text.Json.Nodes.JsonArray(LearningSiteFixture.Role("PathEditor", "AccessAdminPanel", "EditContent", "ViewContent")),
            }),
        }));
        await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, editorName, "PathEditor"));

        // Act
        using var learner = await _fixture.CreateClientAsync("learner1");
        var page = await learner.GetStringAsync($"Contents/ContentItems/{pathId}", TestContext.Current.CancellationToken);

        using var admin = await _fixture.CreateClientAsync("admin");
        var people = await admin.GetAsync($"Admin/learning/paths/{pathId}/people", TestContext.Current.CancellationToken);

        using var editor = await _fixture.CreateClientAsync(editorName);
        var refused = await editor.GetAsync($"Admin/learning/paths/{pathId}/people", TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("Start this path", page);
        Assert.Equal(HttpStatusCode.OK, people.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
    }
}
