using System.Net;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Learners choose how each kind of notice reaches them on the site, without the admin, in the Notifications module's
/// own preferences; and a request to enroll in a course that needs approval reaches the people who may approve it.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningNotificationFollowUpsTests
{
    private readonly LearningSiteFixture _fixture;

    public LearningNotificationFollowUpsTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ALearner_ChoosesTheirNotificationPreferencesOnTheSite_AndTheyAreTheNotificationsModulesOwn()
    {
        // Arrange: a learner of this test only, who has no admin.
        var userName = "prefs" + Guid.NewGuid().ToString("N")[..8];
        var userId = await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, userName));
        using var learner = await _fixture.CreateClientAsync(userName);

        // Act: the page lists each Learning notice with the methods; the learner keeps only "due soon" notices.
        var list = await learner.GetStringAsync("notifications", TestContext.Current.CancellationToken);
        var page = await learner.GetAsync("notifications/preferences", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var saved = await learner.PostAsync("notifications/preferences", new FormUrlEncodedContent(
        [
            new("EnabledMethods", TestNotificationMethodProvider.MethodName),
            new($"TypeMethods[{LearningConstants.NotificationTypes.DueSoon}]", TestNotificationMethodProvider.MethodName),
            new("__RequestVerificationToken", LearningSiteFixture.Token(html)),
        ]), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("href=\"/notifications/preferences\"", list);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Enrolled in a course", html);
        Assert.Contains("Test delivery", html);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var (enrolled, dueSoon) = await _fixture.InTenantAsync(async services =>
        {
            var user = (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId);
            var part = user.GetOrCreate<UserNotificationPreferencesPart>();
            var types = services.GetRequiredService<IOptions<NotificationTypeOptions>>().Value.Types;

            return (
                NotificationTypePreferences.IsMethodEnabledForType(part, types[LearningConstants.NotificationTypes.Enrolled], TestNotificationMethodProvider.MethodName),
                NotificationTypePreferences.IsMethodEnabledForType(part, types[LearningConstants.NotificationTypes.DueSoon], TestNotificationMethodProvider.MethodName));
        });

        Assert.False(enrolled);
        Assert.True(dueSoon);

        // The page shows the choices back.
        var again = await learner.GetStringAsync("notifications/preferences", TestContext.Current.CancellationToken);
        Assert.Contains($"name=\"TypeMethods[{LearningConstants.NotificationTypes.DueSoon}]\" value=\"{TestNotificationMethodProvider.MethodName}\" checked=\"checked\"", again);
        Assert.DoesNotContain($"name=\"TypeMethods[{LearningConstants.NotificationTypes.Enrolled}]\" value=\"{TestNotificationMethodProvider.MethodName}\" checked=\"checked\"", again);
    }

    [Fact]
    public async Task TheNotificationPreferences_NeedASignedInUser()
    {
        using var visitor = await _fixture.CreateClientAsync();

        var page = await visitor.GetAsync("notifications/preferences", TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task ARequestToEnroll_InACourseWithoutStaff_ReachesThePeopleWhoMayManageEnrollments()
    {
        // Arrange: a learner of this test only asks to join the course without staff.
        var userName = "request" + Guid.NewGuid().ToString("N")[..8];
        var userId = await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, userName));

        // Act
        var enrollmentId = await _fixture.InTenantAsync(async services =>
        {
            var result = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = userId,
                CourseId = _fixture.OtherCourseId,
                Source = LearningConstants.EnrollmentSources.Self,
                SourceId = userId,
                RequiresApproval = true,
            });

            Assert.True(result.Succeeded, result.ErrorCode);

            return result.Enrollment.ItemId;
        });

        // Assert: the administrator, who may manage every enrollment, is told, with a link to the enrollment.
        var adminId = await _fixture.InTenantAsync(async services => await services.GetRequiredService<UserManager<IUser>>().GetUserIdAsync(await services.GetRequiredService<UserManager<IUser>>().FindByNameAsync("admin")));
        var notification = await _fixture.InTenantAsync(async services => Assert.Single(await services.GetRequiredService<INotificationManager>().ListAsync(new NotificationFilter
        {
            Type = LearningConstants.NotificationTypes.EnrollmentRequested,
            ReferenceId = enrollmentId,
        })));

        Assert.Equal(adminId, notification.UserId);
        Assert.Equal($"~/Admin/learning/enrollments/{enrollmentId}", notification.Url);
    }
}
