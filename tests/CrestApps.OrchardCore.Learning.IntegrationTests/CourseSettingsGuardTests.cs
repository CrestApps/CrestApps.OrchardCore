using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The staff and the enrollment mode of a course change only for a course manager or an instructor of the published
/// course, whatever saves the course: the editor, the content API (which loads, updates and publishes through the
/// content manager, as these tests do; the API itself needs a bearer token the test site does not issue) or an import.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CourseSettingsGuardTests
{
    private readonly LearningSiteFixture _fixture;

    public CourseSettingsGuardTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ASignedInPersonWhoMayNotChangeCourseSettings_CannotSaveStaffOrTheEnrollmentMode()
    {
        // Act: learner2 saves the course with themselves as an instructor and enrollment closed, then imports the same.
        await AsUserAsync("learner2", async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.DraftRequired);

            course.Alter<CoursePart>(part =>
            {
                part.Staff = [.. part.Staff, new CourseStaffMember { UserId = _fixture.SpareLearnerId, Role = LearningConstants.CourseStaffRoles.Instructor }];
                part.EnrollmentMode = EnrollmentMode.Closed;
            });

            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);
        });

        await AsUserAsync("learner2", async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var imported = await contentManager.CloneAsync(await contentManager.GetAsync(_fixture.CourseId, VersionOptions.Published));

            imported.ContentItemId = _fixture.CourseId;
            imported.Alter<CoursePart>(part =>
            {
                part.Staff = [new CourseStaffMember { UserId = _fixture.SpareLearnerId, Role = LearningConstants.CourseStaffRoles.Instructor }];
                part.EnrollmentMode = EnrollmentMode.Closed;
            });

            await contentManager.ImportAsync([imported]);
        });

        // Assert
        var stored = await _fixture.InTenantAsync(async services => (await services.GetRequiredService<IContentManager>().GetAsync(_fixture.CourseId, VersionOptions.Published)).Get<CoursePart>(nameof(CoursePart)));
        Assert.Equal(EnrollmentMode.Open, stored.EnrollmentMode);
        Assert.DoesNotContain(stored.Staff, member => member.UserId == _fixture.SpareLearnerId);
        Assert.Contains(stored.Staff, member => member.UserId == _fixture.InstructorId);
    }

    [Fact]
    public async Task ACourseManager_ChangesStaffAndTheEnrollmentMode()
    {
        // Act
        await AsUserAsync("admin", async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.GetAsync(_fixture.OtherCourseId, VersionOptions.DraftRequired);

            course.Alter<CoursePart>(part =>
            {
                part.Staff = [new CourseStaffMember { UserId = _fixture.SpareLearnerId, Role = LearningConstants.CourseStaffRoles.Observer }];
                part.EnrollmentMode = EnrollmentMode.Approval;
            });

            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);
        });

        // Assert
        var stored = await _fixture.InTenantAsync(async services => (await services.GetRequiredService<IContentManager>().GetAsync(_fixture.OtherCourseId, VersionOptions.Published)).Get<CoursePart>(nameof(CoursePart)));
        Assert.Equal(EnrollmentMode.Approval, stored.EnrollmentMode);
        Assert.Contains(stored.Staff, member => member.UserId == _fixture.SpareLearnerId);
    }

    private Task<bool> AsUserAsync(string userName, Func<IServiceProvider, Task> work)
        => _fixture.InTenantAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = await userManager.FindByNameAsync(userName);
            var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);

            services.GetRequiredService<IHttpContextAccessor>().HttpContext!.User = principal;

            await work(services);

            return true;
        });
}
