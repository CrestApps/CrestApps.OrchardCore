using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// My learning is a course overview, as Moodle's: a card per course with its cover or a pattern, the progress and the
/// next step; filters by state, a search and a card or list view.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class MyLearningCardsTests
{
    private readonly LearningSiteFixture _fixture;

    public MyLearningCardsTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyLearning_ShowsACardPerCourse_AndFiltersSearchesAndSwitchesToAList()
    {
        // Arrange: one course completed, one not started.
        var userName = "cards" + Guid.NewGuid().ToString("n")[..8];

        await _fixture.InTenantAsync(async services =>
        {
            var userId = await LearningSiteFixture.CreateUserAsync(services, userName);
            var enrollments = services.GetRequiredService<IEnrollmentService>();
            var done = await enrollments.EnrollAsync(new EnrollmentRequest { UserId = userId, CourseId = _fixture.CourseId, Source = LearningConstants.EnrollmentSources.Manual, SourceId = "cards" });
            await enrollments.EnrollAsync(new EnrollmentRequest { UserId = userId, CourseId = _fixture.OtherCourseId, Source = LearningConstants.EnrollmentSources.Manual, SourceId = "cards" });
            await enrollments.ChangeStatusAsync(done.Enrollment.ItemId, EnrollmentStatus.Completed, "admin", "Completed on paper");

            return true;
        });
        using var learner = await _fixture.CreateClientAsync(userName);

        // Act
        var all = await learner.GetStringAsync("learning", TestContext.Current.CancellationToken);
        var completed = await learner.GetStringAsync("learning?status=completed", TestContext.Current.CancellationToken);
        var notStarted = await learner.GetStringAsync("learning?status=notstarted", TestContext.Current.CancellationToken);
        var searched = await learner.GetStringAsync("learning?q=forklift", TestContext.Current.CancellationToken);
        var list = await learner.GetStringAsync("learning?view=list", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("data-course-title=\"Workplace Safety\"", all);
        Assert.Contains("data-course-title=\"Forklift Basics\"", all);
        Assert.Contains("data-learning-course-card", all);
        Assert.Contains("data-course-pattern", all);
        Assert.Contains("data-course-title=\"Workplace Safety\"", completed);
        Assert.DoesNotContain("data-course-title=\"Forklift Basics\"", completed);
        Assert.Contains("data-course-title=\"Forklift Basics\"", notStarted);
        Assert.DoesNotContain("data-course-title=\"Workplace Safety\"", notStarted);
        Assert.Contains("data-course-title=\"Forklift Basics\"", searched);
        Assert.DoesNotContain("data-course-title=\"Workplace Safety\"", searched);
        Assert.Contains("data-learning-course-row", list);
        Assert.DoesNotContain("data-learning-course-card", list);
    }
}
