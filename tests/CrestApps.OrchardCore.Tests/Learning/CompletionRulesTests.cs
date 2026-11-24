using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// The completion rules Moodle courses need: manual completion by a named role of the course staff, a course with no
/// completion criteria that never completes, and an item that completes when its score reaches a minimum whatever
/// the package says it passed.
/// </summary>
public sealed class CompletionRulesTests
{
    [Fact]
    public async Task StaffApproval_WithARole_IsTakenOnlyFromCourseStaffInThatRole()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", part =>
        {
            part.Staff =
            [
                new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor },
                new CourseStaffMember { UserId = "helper", Role = LearningConstants.CourseStaffRoles.Assistant },
            ];
            part.CompletionCriteria = [new LearningRuleSettings { Name = LearningRuleNames.StaffApproval, Values = { ["Role"] = LearningConstants.CourseStaffRoles.Instructor } }];
        });
        courses.AddItem("course1", "intro");
        var enrollment = await EnrollAsync(harness, "learner", "course1");
        await Progress(harness, (service, e) => service.RecordAccessAsync(e, "intro", 30), enrollment.ItemId);

        // Act
        var byAssistant = await Progress(harness, (service, e) => service.ApproveCompletionAsync(e, "helper"), enrollment.ItemId);
        var byStranger = await Progress(harness, (service, e) => service.ApproveCompletionAsync(e, "admin"), enrollment.ItemId);
        var byInstructor = await Progress(harness, (service, e) => service.ApproveCompletionAsync(e, "teacher"), enrollment.ItemId);

        // Assert
        Assert.Equal(LearningRefusals.NotApprover, byAssistant.RefusedReason);
        Assert.Equal(LearningRefusals.NotApprover, byStranger.RefusedReason);
        Assert.Null(byInstructor.RefusedReason);
        Assert.True(byInstructor.CourseCompleted);
    }

    [Theory]
    [InlineData(null, "admin", true)]
    [InlineData("Instructor", "teacher", true)]
    [InlineData("Instructor", "helper", false)]
    [InlineData("instructor", "teacher", true)]
    [InlineData("Instructor", null, false)]
    public void CompletionApprovers_TellWhoMayApprove(string role, string userId, bool allowed)
    {
        // Arrange
        var part = new CoursePart
        {
            Staff =
            [
                new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor },
                new CourseStaffMember { UserId = "helper", Role = LearningConstants.CourseStaffRoles.Assistant },
            ],
        };
        var rule = new LearningRuleSettings { Name = LearningRuleNames.StaffApproval };

        if (role is not null)
        {
            rule.Values["Role"] = role;
        }

        part.CompletionCriteria = [rule];

        // Act
        var result = CompletionApprovers.MayApprove(part, userId);

        // Assert
        Assert.Equal(allowed, result);
    }

    [Fact]
    public async Task ACourseWithoutCompletionCriteria_NeverCompletes_ButKeepsItsProgress()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", part => part.NeverCompletes = true);
        courses.AddItem("course1", "intro");
        courses.AddItem("course1", "policy");
        var enrollment = await EnrollAsync(harness, "learner", "course1");

        // Act
        await Progress(harness, (service, e) => service.RecordAccessAsync(e, "intro", 30), enrollment.ItemId);
        var last = await Progress(harness, (service, e) => service.RecordAccessAsync(e, "policy", 30), enrollment.ItemId);

        // Assert
        Assert.True(last.ItemCompleted);
        Assert.False(last.CourseCompleted);
        Assert.Equal(EnrollmentStatus.Active, last.Enrollment.Status);
        Assert.Equal(100, last.Enrollment.ProgressPercent);
    }

    [Fact]
    public async Task MinimumScore_CompletesWhenTheScoreReachesThePassMark_WhateverThePassedFlagSays()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "package", completionMode: CompletionModes.MinimumScore, configure: part => part.PassMark = 80);
        courses.AddItem("course1", "other");
        var enrollment = await EnrollAsync(harness, "learner", "course1");

        // Act
        var low = await Progress(harness, (service, e) => service.RecordResultAsync(e, new CourseItemResult { ItemId = "package", Score = 70, Passed = true, ReportedComplete = true }), enrollment.ItemId);
        var noScore = await Progress(harness, (service, e) => service.RecordResultAsync(e, new CourseItemResult { ItemId = "package", Passed = true, ReportedComplete = true }), enrollment.ItemId);
        var high = await Progress(harness, (service, e) => service.RecordResultAsync(e, new CourseItemResult { ItemId = "package", Score = 85, Passed = false }), enrollment.ItemId);

        // Assert
        Assert.False(low.ItemCompleted);
        Assert.NotEqual(CourseItemState.Failed, low.Item.State);
        Assert.False(noScore.ItemCompleted);
        Assert.True(high.ItemCompleted);
        Assert.Equal(CourseItemState.Completed, high.Item.State);
    }

    [Fact]
    public void ThePackageKind_OffersTheMinimumScoreMode()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLearningPackagesCore();
        CrestApps.OrchardCore.Tests.Learning.Scorm.PackageArchiveCacheTestServices.Use(services);

        // Act
        var kind = services.BuildServiceProvider().GetRequiredService<Microsoft.Extensions.Options.IOptions<CourseItemKindOptions>>().Value.Find(CrestApps.OrchardCore.Learning.Packages.PackagesConstants.PackageKind);

        // Assert
        Assert.Contains(CompletionModes.MinimumScore, kind.CompletionModes);
    }

    private static async Task<Enrollment> EnrollAsync(CalendarTestHarness harness, string userId, string courseId)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
        }));

        Assert.True(result.Succeeded, result.ErrorCode);

        return result.Enrollment;
    }

    private static Task<ProgressUpdate> Progress(CalendarTestHarness harness, Func<ILearnerProgressService, Enrollment, Task<ProgressUpdate>> action, string enrollmentId)
        => harness.RunAsync(async services =>
        {
            var enrollment = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollmentId);

            return await action(services.GetRequiredService<ILearnerProgressService>(), enrollment);
        });
}
