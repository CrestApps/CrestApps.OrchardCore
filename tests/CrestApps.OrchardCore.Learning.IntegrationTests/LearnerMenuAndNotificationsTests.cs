using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// A learner reaches their courses from My courses in the user menu, gets Learning notices through Orchard Core's
/// Notifications module with their notification type, and reads them on the site's Notifications page without the
/// admin, opening each through the module's own link, which marks it read.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed partial class LearnerMenuAndNotificationsTests
{
    private readonly LearningSiteFixture _fixture;

    public LearnerMenuAndNotificationsTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheUserMenu_ListsTheLearnersActiveCourses_UnderMyCourses()
    {
        using var learner = await _fixture.CreateClientAsync("learner1");

        var page = await learner.GetStringAsync("learning", TestContext.Current.CancellationToken);

        Assert.Contains("My courses", page);
        Assert.Contains($"href=\"/learning/courses/{_fixture.CourseId}/play\"", page);
        Assert.Contains("Workplace Safety", page);
    }

    [Fact]
    public async Task TheAdminUserMenu_HasMyCourses_Too()
    {
        using var admin = await _fixture.CreateClientAsync("admin");

        var page = await admin.GetStringAsync("Admin", TestContext.Current.CancellationToken);

        Assert.Contains("My courses", page);
        Assert.Contains("href=\"/learning\"", page);
    }

    [Fact]
    public async Task AnEnrollment_ReachesTheLearner_ThroughNotifications_WithItsType_AndTheLearnerReadsItOnTheSite()
    {
        // Arrange: a learner of this test only, enrolled in the course without staff.
        var userName = "notice" + Guid.NewGuid().ToString("N")[..8];
        var userId = await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, userName));

        // Act
        await _fixture.InTenantAsync(async services =>
        {
            var result = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = userId,
                CourseId = _fixture.OtherCourseId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "notice-test",
            });

            Assert.True(result.Succeeded, result.ErrorCode);
        });

        // Assert: the notice is in the Notifications store, typed and linked site-relative.
        var notification = await _fixture.InTenantAsync(async services => Assert.Single(await services.GetRequiredService<INotificationManager>().ListAsync(new NotificationFilter
        {
            UserId = userId,
            Type = LearningConstants.NotificationTypes.Enrolled,
        })));

        Assert.Equal($"~/learning/courses/{_fixture.OtherCourseId}/play", notification.Url);

        // The learner, who has no admin, reads it on the site's Notifications page.
        using var learner = await _fixture.CreateClientAsync(userName);
        Assert.NotEqual(HttpStatusCode.OK, (await learner.GetAsync("Admin/notifications", TestContext.Current.CancellationToken)).StatusCode);

        var page = await learner.GetAsync("notifications", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("You are enrolled in Forklift Basics", html);
        Assert.Contains($"/Notifications/Open/{notification.NotificationId}", html);
        Assert.Contains("data-is-read=\"false\"", html);

        // The user menu of the site's pages links to it.
        Assert.Contains("href=\"/notifications\"", await learner.GetStringAsync("learning", TestContext.Current.CancellationToken));

        // Opening it through the Notifications module's link marks it read and goes to the course.
        var open = await learner.GetAsync($"Notifications/Open/{notification.NotificationId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, open.StatusCode);
        Assert.Equal($"/learning/courses/{_fixture.OtherCourseId}/play", open.Headers.Location?.OriginalString);

        var unread = await learner.GetStringAsync("notifications?status=unread", TestContext.Current.CancellationToken);

        Assert.DoesNotContain(notification.NotificationId, unread);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksTheLearnersOwnNotificationsRead()
    {
        // Arrange: two notices for a learner of this test only.
        var userName = "markall" + Guid.NewGuid().ToString("N")[..8];
        var userId = await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, userName));
        await _fixture.InTenantAsync(async services =>
        {
            var enrollments = services.GetRequiredService<IEnrollmentService>();

            foreach (var courseId in new[] { _fixture.CourseId, _fixture.OtherCourseId })
            {
                Assert.True((await enrollments.EnrollAsync(new EnrollmentRequest { UserId = userId, CourseId = courseId, Source = LearningConstants.EnrollmentSources.Manual, SourceId = "mark-all-test" })).Succeeded);
            }
        });

        using var learner = await _fixture.CreateClientAsync(userName);
        var page = await learner.GetStringAsync("notifications", TestContext.Current.CancellationToken);

        // Act
        var marked = await learner.PostAsync("notifications/mark-all-read", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(page),
        }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, marked.StatusCode);

        var unread = await _fixture.InTenantAsync(services => services.GetRequiredService<INotificationManager>().ListAsync(new NotificationFilter { UserId = userId, IsRead = false }));

        Assert.Empty(unread);
        Assert.DoesNotContain("data-is-read=\"false\"", await learner.GetStringAsync("notifications", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheNotificationsPage_AsksAnonymousVisitorsToSignIn()
    {
        using var anonymous = await _fixture.CreateClientAsync();

        var response = await anonymous.GetAsync("notifications", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Matches(LoginPath(), response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [GeneratedRegex("Login", RegexOptions.IgnoreCase)]
    private static partial Regex LoginPath();
}
