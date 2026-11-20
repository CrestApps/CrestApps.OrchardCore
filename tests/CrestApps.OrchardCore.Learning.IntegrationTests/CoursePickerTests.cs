using System.Net;
using System.Text.Json;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The course pickers of the admin (course prerequisites, the courses of a learning path set, the courses of a workflow
/// event) search the courses on the server, as a site with hundreds of courses needs, and render only the chosen ones.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CoursePickerTests
{
    private readonly LearningSiteFixture _fixture;

    public CoursePickerTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheCourseSearch_FindsCoursesByTitle_LeavesOutTheCourseBeingEdited_AndIsRefusedToLearners()
    {
        // Arrange
        using var admin = await _fixture.CreateClientAsync("admin");
        using var learner = await _fixture.CreateClientAsync("learner1");

        // Act
        var found = await admin.GetAsync("Admin/learning/course-picker/search?query=Forklift", TestContext.Current.CancellationToken);
        var excepted = await admin.GetAsync($"Admin/learning/course-picker/search?exceptId={_fixture.OtherCourseId}&query=Forklift", TestContext.Current.CancellationToken);
        var refused = await learner.GetAsync("Admin/learning/course-picker/search?query=Forklift", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);

        using (var json = JsonDocument.Parse(await found.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)))
        {
            var item = Assert.Single(json.RootElement.EnumerateArray(), element => element.GetProperty("value").GetString() == _fixture.OtherCourseId);
            Assert.Equal("Forklift Basics", item.GetProperty("text").GetString());
            Assert.DoesNotContain(json.RootElement.EnumerateArray(), element => element.GetProperty("value").GetString() == _fixture.CourseId);
        }

        using (var json = JsonDocument.Parse(await excepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)))
        {
            Assert.DoesNotContain(json.RootElement.EnumerateArray(), element => element.GetProperty("value").GetString() == _fixture.OtherCourseId);
        }

        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
    }

    [Fact]
    public async Task TheCourseEditor_SearchesPrerequisites_AndRendersOnlyTheChosenOnes()
    {
        using var admin = await _fixture.CreateClientAsync("admin");

        var html = await admin.GetStringAsync($"Admin/Contents/ContentItems/{_fixture.CourseId}/Edit", TestContext.Current.CancellationToken);

        Assert.Contains("data-item-selector", html);
        Assert.Contains("/Admin/learning/course-picker/search", html);
        Assert.DoesNotContain($"<option value=\"{_fixture.OtherCourseId}\"", html);
    }

    [Fact]
    public async Task TheLearningPathEditor_SearchesTheCoursesOfEachSet()
    {
        using var admin = await _fixture.CreateClientAsync("admin");

        var html = await admin.GetStringAsync("Admin/Contents/ContentTypes/LearningPath/Create", TestContext.Current.CancellationToken);

        Assert.Contains("data-item-selector", html);
        Assert.Contains("/Admin/learning/course-picker/search", html);
        Assert.DoesNotContain($"<option value=\"{_fixture.OtherCourseId}\"", html);
        Assert.DoesNotContain($"<option value=\"{_fixture.CourseId}\"", html);
    }
}
