using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core;
using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Course staff come from the published course only, and only a course manager or an instructor of the published course
/// who holds Manage own courses may change the staff, the enrollment mode and the key.
/// </summary>
public sealed class CourseSettingsPolicyTests
{
    [Fact]
    public async Task Staff_IsReadFromThePublishedCourse_NotFromADraft()
    {
        // Arrange
        var (harness, _, _) = await LearningTestHarness.CreateAsync(
            extraSchema: LearningIndexSchema.CreateCourseStaffIndexAsync,
            extraIndexProviders: [new CourseStaffIndexProvider()]);
        await using var __ = harness;

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<global::YesSql.ISession>();
            await session.SaveAsync(Course("course1-v1", published: true, latest: false, "teacher"));
            await session.SaveAsync(Course("course1-v2", published: false, latest: true, "teacher", "intruder"));

            return true;
        });

        // Act
        var (teacher, intruder, courses) = await harness.RunAsync(async services =>
        {
            var structure = ActivatorUtilities.CreateInstance<DefaultCourseStructureService>(services, Mock.Of<IContentManager>());

            return (await structure.IsCourseStaffAsync("teacher", "course1"), await structure.IsCourseStaffAsync("intruder", "course1"), await structure.GetStaffCourseIdsAsync("intruder"));
        }, commit: false);

        // Assert
        Assert.True(teacher);
        Assert.False(intruder);
        Assert.Empty(courses);
    }

    [Theory]
    [InlineData("manager", true, false, true)]
    [InlineData("teacher", false, true, true)]
    [InlineData("teacher", false, false, false)]
    [InlineData("assistant", false, true, false)]
    [InlineData("editor", false, true, false)]
    public async Task ChangingEnrollmentSettings_NeedsManageCourses_OrAnInstructorWithManageOwnCourses(string userId, bool manageCourses, bool manageOwn, bool allowed)
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", part => part.Staff =
        [
            new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor },
            new CourseStaffMember { UserId = "assistant", Role = LearningConstants.CourseStaffRoles.Assistant },
        ]);
        var permissions = new List<string> { CommonPermissions.EditContent.Name };

        if (manageCourses)
        {
            permissions.Add(LearningPermissions.ManageCourses.Name);
        }

        if (manageOwn)
        {
            permissions.Add(LearningPermissions.ManageOwnCourses.Name);
        }

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<CourseSettingsPolicy>().CanChangeEnrollmentSettingsAsync(CalendarTestHarness.User(userId, [.. permissions]), "course1"), commit: false);

        // Assert
        Assert.Equal(allowed, result);
    }

    private static ContentItem Course(string versionId, bool published, bool latest, params string[] staff)
    {
        var course = new ContentItem
        {
            ContentItemId = "course1",
            ContentItemVersionId = versionId,
            ContentType = LearningConstants.ContentTypes.Course,
            Published = published,
            Latest = latest,
        };

        course.Alter<CoursePart>(part => part.Staff = staff.Select(user => new CourseStaffMember { UserId = user, Role = LearningConstants.CourseStaffRoles.Instructor }).ToList());

        return course;
    }
}
