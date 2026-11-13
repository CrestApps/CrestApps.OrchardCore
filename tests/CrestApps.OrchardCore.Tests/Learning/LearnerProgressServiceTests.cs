using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class LearnerProgressServiceTests
{
    [Fact]
    public async Task RecordAccessAsync_OnAViewedItem_CompletesIt_AndTheLastRequiredItemCompletesTheCourse()
    {
        // Arrange
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync();
        await using var _ = harness;
        courses.AddCourse("course1", part => part.Code = "SAFE-101", title: "Safety");
        courses.AddItem("course1", "intro");
        courses.AddItem("course1", "extra", required: false);
        courses.AddItem("course1", "policy");
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var first = await Progress(harness, (service, e) => service.RecordAccessAsync(e, "intro", 30), enrollment.ItemId);
        var last = await Progress(harness, (service, e) => service.RecordAccessAsync(e, "policy", 45), enrollment.ItemId);

        // Assert
        Assert.True(first.ItemCompleted);
        Assert.False(first.CourseCompleted);
        Assert.Equal(50, first.Enrollment.ProgressPercent);
        Assert.True(last.CourseCompleted);

        var completed = await FindAsync(harness, enrollment.ItemId);

        Assert.Equal(EnrollmentStatus.Completed, completed.Status);
        Assert.Equal(100, completed.ProgressPercent);
        Assert.Equal(LearningTestHarness.Now, completed.StartedUtc);
        Assert.Equal("Safety", completed.Completion.CourseTitle);
        Assert.Equal("SAFE-101", completed.Completion.CourseCode);
        Assert.Equal("course1-v1", completed.Completion.CourseVersionId);
        Assert.Equal(75, completed.Completion.SecondsSpent);
        Assert.Equal(3, completed.Completion.Items.Count);
        Assert.Equal("Item intro", completed.Completion.Items[0].Title);
        Assert.Equal(CourseItemState.NotStarted, completed.Completion.Items.Single(item => item.ContentItemId == "extra").State);
        Assert.Equal(["Enrolled", "Started", "ItemCompleted:intro", "ItemCompleted:policy", "Status:Active->Completed", "Completed"], observer.Events);
    }

    [Fact]
    public async Task RecordAccessAsync_OnAMinimumTimeItem_CompletesOnceTheTimeIsSpent_AndCapsEachCall()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "reading", completionMode: CompletionModes.MinimumTime, configure: part => part.MinimumMinutes = 2);
        courses.AddItem("course1", "other");
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var capped = await Progress(harness, (service, e) => service.RecordAccessAsync(e, "reading", 100_000), enrollment.ItemId);
        var second = await Progress(harness, (service, e) => service.RecordAccessAsync(e, "reading", 60), enrollment.ItemId);

        // Assert
        Assert.False(capped.ItemCompleted);
        Assert.Equal(60, capped.Item.SecondsSpent);
        Assert.True(second.ItemCompleted);
        Assert.Equal(120, second.Item.SecondsSpent);
    }

    [Fact]
    public async Task RecordViewedAsync_CompletesAVideoAtTheRequiredShare()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        var video = courses.AddItem("course1", "video", LearningConstants.CourseItemKinds.Video, CompletionModes.Watched);
        video.Alter<VideoLessonPart>(part => part.RequiredPercent = 80);
        courses.AddItem("course1", "other");
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var partly = await Progress(harness, (service, e) => service.RecordViewedAsync(e, "video", 50), enrollment.ItemId);
        var lower = await Progress(harness, (service, e) => service.RecordViewedAsync(e, "video", 20), enrollment.ItemId);
        var enough = await Progress(harness, (service, e) => service.RecordViewedAsync(e, "video", 85), enrollment.ItemId);

        // Assert
        Assert.False(partly.ItemCompleted);
        Assert.Equal(50, lower.Item.PercentViewed);
        Assert.True(enough.ItemCompleted);
    }

    [Fact]
    public async Task MarkDoneAsync_CompletesOnlyItemsMarkedDoneByTheLearner()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "task", completionMode: CompletionModes.MarkedDone);
        courses.AddItem("course1", "quiz", completionMode: CompletionModes.Passed);
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var done = await Progress(harness, (service, e) => service.MarkDoneAsync(e, "task"), enrollment.ItemId);
        var refused = await Progress(harness, (service, e) => service.MarkDoneAsync(e, "quiz"), enrollment.ItemId);

        // Assert
        Assert.True(done.ItemCompleted);
        Assert.False(refused.ItemCompleted);
        Assert.NotNull(refused.RefusedReason);
    }

    [Fact]
    public async Task AcknowledgeAsync_KeepsTheStatementAndVersionAsShown_AndNeverOverwritesThem()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        var policy = courses.AddItem("course1", "policy", LearningConstants.CourseItemKinds.Acknowledgement, CompletionModes.Acknowledged);
        policy.Alter<AcknowledgementPart>(part => part.Statement = "I have read the data policy.");
        courses.AddItem("course1", "other");
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var first = await Progress(harness, (service, e) => service.AcknowledgeAsync(e, "policy"), enrollment.ItemId);
        policy.Alter<AcknowledgementPart>(part => part.Statement = "Changed text.");
        policy.ContentItemVersionId = "policy-v2";
        var again = await Progress(harness, (service, e) => service.AcknowledgeAsync(e, "policy"), enrollment.ItemId);

        // Assert
        Assert.True(first.ItemCompleted);
        Assert.Equal("I have read the data policy.", again.Item.AttestedStatement);
        Assert.Equal("policy-v1", again.Item.AttestedVersionId);
        Assert.Equal(LearningTestHarness.Now, again.Item.AttestedUtc);
    }

    [Fact]
    public async Task RecordResultAsync_OnAPassedItem_FailsBelowThePassMark_AndCompletesAtIt()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "quiz", "Quiz", CompletionModes.Passed, configure: part =>
        {
            part.PassMark = 70;
            part.Gradable = true;
        });
        courses.AddItem("course1", "other");
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var failed = await Progress(harness, (service, e) => service.RecordResultAsync(e, new CourseItemResult { ItemId = "quiz", Score = 60, Attempts = 1 }), enrollment.ItemId);
        var passed = await Progress(harness, (service, e) => service.RecordResultAsync(e, new CourseItemResult { ItemId = "quiz", Score = 70, Attempts = 2 }), enrollment.ItemId);

        // Assert
        Assert.Equal(CourseItemState.Failed, failed.Item.State);
        Assert.False(failed.Item.Passed);
        Assert.Equal(CourseItemState.Completed, passed.Item.State);
        Assert.True(passed.Item.Passed);
        Assert.Equal(2, passed.Item.Attempts);
        Assert.Equal(70, passed.Enrollment.Grade);
    }

    [Fact]
    public async Task RecordResultAsync_KeepsTheSelfReportedMark()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "package", "Package", CompletionModes.PackageReported);
        courses.AddItem("course1", "other");
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var update = await Progress(harness, (service, e) => service.RecordResultAsync(e, new CourseItemResult { ItemId = "package", ReportedComplete = true, SelfReported = true, Score = 90 }), enrollment.ItemId);

        // Assert
        Assert.True(update.ItemCompleted);
        Assert.True(update.Item.SelfReported);
    }

    [Fact]
    public async Task Progress_OnAnItemThatIsNotReleased_IsRefused()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "first");
        courses.AddItem("course1", "second", configure: part => part.ReleaseRules =
        [
            new LearningRuleSettings { Name = LearningRuleNames.ItemCompleted, Values = { ["ItemId"] = "first" } },
        ]);
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var locked = await Progress(harness, (service, e) => service.RecordAccessAsync(e, "second", 10), enrollment.ItemId);
        await Progress(harness, (service, e) => service.RecordAccessAsync(e, "first", 10), enrollment.ItemId);
        var open = await Progress(harness, (service, e) => service.RecordAccessAsync(e, "second", 10), enrollment.ItemId);

        // Assert
        Assert.NotNull(locked.RefusedReason);
        Assert.Null(locked.Item);
        Assert.True(open.ItemCompleted);
    }

    [Fact]
    public async Task Progress_OnASuspendedEnrollment_IsRefused()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "intro");
        var enrollment = await EnrollAsync(harness, "user1", "course1");
        await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(enrollment.ItemId, EnrollmentStatus.Suspended, "admin", "Paused"));

        // Act
        var update = await Progress(harness, (service, e) => service.RecordAccessAsync(e, "intro", 10), enrollment.ItemId);

        // Assert
        Assert.NotNull(update.RefusedReason);
        Assert.Equal(EnrollmentStatus.Suspended, (await FindAsync(harness, enrollment.ItemId)).Status);
    }

    [Fact]
    public async Task CourseCompletion_WaitsForTheMinimumGrade()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", part =>
        {
            part.PassMark = 80;
            part.CompletionCriteria = [new LearningRuleSettings { Name = LearningRuleNames.MinimumGrade, Values = { ["Grade"] = "80" } }];
        });
        courses.AddItem("course1", "quiz", "Quiz", CompletionModes.Graded, configure: part => part.Gradable = true);
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var low = await Progress(harness, (service, e) => service.RecordResultAsync(e, new CourseItemResult { ItemId = "quiz", Score = 75, Graded = true }), enrollment.ItemId);
        var high = await Progress(harness, (service, e) => service.RecordResultAsync(e, new CourseItemResult { ItemId = "quiz", Score = 85, Graded = true }), enrollment.ItemId);

        // Assert
        Assert.True(low.ItemCompleted);
        Assert.False(low.CourseCompleted);
        Assert.False(low.Enrollment.Passed);
        Assert.True(high.CourseCompleted);
        Assert.True(high.Enrollment.Passed);
        Assert.Equal(85, (await FindAsync(harness, enrollment.ItemId)).Completion.Grade);
    }

    [Fact]
    public async Task CompleteItemAsync_ByHand_RecordsTheActorAndReason()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lab", completionMode: CompletionModes.Attended);
        courses.AddItem("course1", "other");
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var update = await Progress(harness, (service, e) => service.CompleteItemAsync(e, "lab", "instructor1", "Signed the paper roster"), enrollment.ItemId);

        // Assert
        Assert.True(update.ItemCompleted);
        Assert.Equal("instructor1", update.Item.CompletedBy);
        Assert.Equal("Signed the paper roster", update.Item.CompletionReason);
    }

    [Fact]
    public async Task ApproveCompletionAsync_CompletesACourseThatWaitsForTheStaff_AndKeepsWhoApproved()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", part => part.CompletionCriteria = [new LearningRuleSettings { Name = LearningRuleNames.StaffApproval }]);
        courses.AddItem("course1", "lesson");
        var enrollment = await EnrollAsync(harness, "user1", "course1");

        // Act
        var viewed = await Progress(harness, (service, e) => service.RecordAccessAsync(e, "lesson", 5), enrollment.ItemId);
        var approved = await Progress(harness, (service, e) => service.ApproveCompletionAsync(e, "instructor1"), enrollment.ItemId);

        // Assert
        Assert.True(viewed.ItemCompleted);
        Assert.False(viewed.CourseCompleted);
        Assert.True(approved.CourseCompleted);

        var completed = await FindAsync(harness, enrollment.ItemId);

        Assert.Equal(EnrollmentStatus.Completed, completed.Status);
        Assert.True(completed.Properties.ContainsKey(LearningRuleNames.StaffApprovalProperty));
        Assert.Equal("instructor1", completed.History.Last().Actor);
    }

    [Fact]
    public async Task RecordAccessAsync_WithACopyReadInAnotherScope_UpdatesTheSameDocument()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson");
        var copy = await EnrollAsync(harness, "user1", "course1");

        // Act
        await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(copy, "lesson", 10));

        // Assert
        var enrollments = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByUserAsync("user1"), commit: false);
        var only = Assert.Single(enrollments);

        Assert.Equal(EnrollmentStatus.Completed, only.Status);
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

    private static Task<Enrollment> FindAsync(CalendarTestHarness harness, string id)
        => harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(id), commit: false);
}
