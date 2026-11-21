using System.Security.Claims;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Learning plan Phase 7 and section 9: a submitted file opens only to the learner who handed it in and to the people
/// who may grade their work in the course; another learner is refused, and a link that pairs a file with another
/// submission opens nothing.
/// </summary>
public sealed class CourseworkFilesPolicyTests
{
    [Theory]
    [InlineData("ana", false, true)]
    [InlineData("ben", false, false)]
    [InlineData("site-grader", true, true)]
    [InlineData("course-grader", false, true)]
    [InlineData("other-grader", false, false)]
    public async Task OnlyTheLearner_AndTheirGraders_OpenASubmittedFile(string userId, bool siteGrader, bool granted)
    {
        // Arrange
        await using var h = await CreateAsync();
        var user = siteGrader
            ? CalendarTestHarness.User(userId, AssessmentPermissions.GradeAssessments.Name)
            : CalendarTestHarness.User(userId, LearningPermissions.ManageOwnCourses.Name, LearningPermissions.AccessLearning.Name);

        // Act
        var canRead = await CanReadAsync(h, user, "s1", "file1");

        // Assert
        Assert.Equal(granted, canRead);
    }

    [Fact]
    public async Task AFileOfAnotherSubmission_IsRefused_EvenToItsOwnLearner()
    {
        // Arrange
        await using var h = await CreateAsync();

        // Act
        var canRead = await CanReadAsync(h, CalendarTestHarness.User("ana"), "s1", "file-of-ben");

        // Assert
        Assert.False(canRead);
    }

    [Fact]
    public async Task NobodySignedIn_IsRefused()
    {
        // Arrange
        await using var h = await CreateAsync();

        // Act
        var canRead = await CanReadAsync(h, null, "s1", "file1");

        // Assert
        Assert.False(canRead);
    }

    private static Task<bool> CanReadAsync(CourseworkTestHarness h, ClaimsPrincipal user, string submissionId, string fileId)
        => h.RunAsync(async services =>
        {
            foreach (var policy in services.GetServices<IProtectedFileAccessPolicy<CourseworkFiles>>())
            {
                if (!await policy.CanReadAsync(new ProtectedFileAccess { FileId = fileId, ResourceId = submissionId, User = user }))
                {
                    return false;
                }
            }

            return true;
        });

    private static async Task<CourseworkTestHarness> CreateAsync()
    {
        var h = await CourseworkTestHarness.CreateAsync();

        h.Courses.AddCourse("course1", part => part.Staff = [new CourseStaffMember { UserId = "course-grader", Role = LearningConstants.CourseStaffRoles.Grader }]);
        h.Courses.AddCourse("course2", part => part.Staff = [new CourseStaffMember { UserId = "other-grader", Role = LearningConstants.CourseStaffRoles.Grader }]);

        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SubmissionStore>();

            await store.CreateAsync(new Submission
            {
                ItemId = "s1",
                UserId = "ana",
                CourseId = "course1",
                EnrollmentId = "e1",
                CourseworkId = "work1",
                SubmittedUtc = LearningTestHarness.Now,
                Files = [new SubmissionFile { FileId = "file1", FileName = "essay.pdf", Size = 10 }],
            });

            await store.CreateAsync(new Submission
            {
                ItemId = "s2",
                UserId = "ben",
                CourseId = "course1",
                EnrollmentId = "e2",
                CourseworkId = "work1",
                SubmittedUtc = LearningTestHarness.Now,
                Files = [new SubmissionFile { FileId = "file-of-ben", FileName = "essay.pdf", Size = 10 }],
            });

            return true;
        });

        return h;
    }
}
