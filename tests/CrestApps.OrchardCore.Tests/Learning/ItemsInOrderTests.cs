using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// The course setting "Learners take the items in order": a required item opens only once every required item before it
/// is complete; optional items never block; the decision comes from the release evaluation, so access and progress
/// refuse a locked item alike.
/// </summary>
public sealed class ItemsInOrderTests
{
    [Fact]
    public async Task InOrder_LocksAnItem_UntilEveryRequiredItemBeforeItIsComplete_AndNamesTheItemToFinish()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var _h = harness;
        courses.AddCourse("course1", part => part.ItemsInOrder = true);
        var first = courses.AddItem("course1", "first", completionMode: CompletionModes.MarkedDone);
        var optional = courses.AddItem("course1", "optional", completionMode: CompletionModes.MarkedDone, required: false);
        var second = courses.AddItem("course1", "second", completionMode: CompletionModes.MarkedDone, sectionId: "s2");
        var enrollment = await EnrollAsync(harness, "learner", "course1");

        // Act
        var firstAccess = await AccessAsync(harness, first);
        var secondAccess = await AccessAsync(harness, second);
        var refused = await ProgressAsync(harness, (service, e) => service.MarkDoneAsync(e, "second"), enrollment.ItemId);

        // Assert
        Assert.Equal(CourseItemAccessReason.Enrolled, firstAccess.Reason);
        Assert.Equal(CourseItemAccessReason.NotReleased, secondAccess.Reason);
        Assert.Equal(LearningRefusals.CompleteEarlierItem, secondAccess.LockedReason);
        Assert.Equal("first", secondAccess.BlockingItemId);
        Assert.Equal(LearningRefusals.CompleteEarlierItem, refused.RefusedReason);

        // The optional item opens once the required items before it are done; it never blocks the next one.
        Assert.Equal(CourseItemAccessReason.NotReleased, (await AccessAsync(harness, optional)).Reason);

        var done = await ProgressAsync(harness, (service, e) => service.MarkDoneAsync(e, "first"), enrollment.ItemId);

        Assert.Null(done.RefusedReason);
        Assert.Equal(CourseItemAccessReason.Enrolled, (await AccessAsync(harness, optional)).Reason);
        Assert.Equal(CourseItemAccessReason.Enrolled, (await AccessAsync(harness, second)).Reason);
        Assert.Null((await ProgressAsync(harness, (service, e) => service.MarkDoneAsync(e, "second"), enrollment.ItemId)).RefusedReason);
    }

    [Fact]
    public async Task WithoutTheSetting_EveryItemIsOpen()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var _h = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "first", completionMode: CompletionModes.MarkedDone);
        var second = courses.AddItem("course1", "second", completionMode: CompletionModes.MarkedDone);
        await EnrollAsync(harness, "learner", "course1");

        // Act
        var access = await AccessAsync(harness, second);

        // Assert
        Assert.Equal(CourseItemAccessReason.Enrolled, access.Reason);
    }

    [Fact]
    public async Task TheItemCompletedRule_NamesTheItemToFinish()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var _h = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson");
        var later = courses.AddItem("course1", "later", configure: part => part.ReleaseRules =
        [
            new LearningRuleSettings { Name = LearningRuleNames.ItemCompleted, Values = { ["ItemId"] = "lesson" } },
        ]);
        await EnrollAsync(harness, "learner", "course1");

        // Act
        var access = await AccessAsync(harness, later);

        // Assert
        Assert.Equal("lesson", access.BlockingItemId);
    }

    private static Task<CourseItemAccess> AccessAsync(CalendarTestHarness harness, ContentItem item)
        => harness.RunAsync(services => services.GetRequiredService<ICourseAccessService>().GetItemAccessAsync(CalendarTestHarness.User("learner"), item), commit: false);

    private static Task<ProgressUpdate> ProgressAsync(CalendarTestHarness harness, Func<ILearnerProgressService, Enrollment, Task<ProgressUpdate>> action, string enrollmentId)
        => harness.RunAsync(async services =>
        {
            var enrollment = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollmentId);

            return await action(services.GetRequiredService<ILearnerProgressService>(), enrollment);
        });

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
}
