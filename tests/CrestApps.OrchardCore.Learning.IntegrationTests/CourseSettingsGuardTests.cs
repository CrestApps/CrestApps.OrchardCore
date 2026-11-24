using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Users;
using YesSql;

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

        string importedVersionId = null;

        await AsUserAsync("learner2", async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();

            // A copy that is not stored, as an import file brings it: CloneAsync stores a new item, and giving that stored
            // clone the course's id left the shared course with two latest versions (review round 9, the Deep Linking
            // test that failed only after this one).
            var published = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.Published);
            var imported = System.Text.Json.JsonSerializer.Deserialize<ContentItem>(System.Text.Json.JsonSerializer.Serialize(published));

            imported.Id = 0;
            imported.ContentItemVersionId = importedVersionId = global::OrchardCore.IdGenerator.GenerateId();
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

        // The import was stored (as the latest version), and the shared course is left with one latest version, so the
        // tests that read its draft after this one see it.
        var latest = await _fixture.InTenantAsync(async services => await services.GetRequiredService<global::YesSql.ISession>()
            .QueryIndex<global::OrchardCore.ContentManagement.Records.ContentItemIndex>(index => index.ContentItemId == _fixture.CourseId && index.Latest)
            .ListAsync());
        Assert.Equal(importedVersionId, Assert.Single(latest).ContentItemVersionId);
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
