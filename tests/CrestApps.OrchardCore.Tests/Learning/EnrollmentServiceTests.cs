using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class EnrollmentServiceTests
{
    [Fact]
    public async Task EnrollAsync_CreatesAnActiveNumberedEnrollmentWithOneGrant()
    {
        // Arrange
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync();
        await using var _ = harness;
        courses.AddCourse("course1");

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course1", "m1")));

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Created);

        var enrollment = await FindAsync(harness, result.Enrollment.ItemId);

        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
        Assert.Equal("user1", enrollment.UserId);
        Assert.Equal("course1", enrollment.CourseId);
        Assert.Equal(1, enrollment.Cycle);
        Assert.False(string.IsNullOrEmpty(enrollment.Number));
        Assert.Single(enrollment.Grants);
        Assert.Equal(LearningTestHarness.Now, enrollment.CreatedUtc);
        Assert.Equal(["Enrolled"], observer.Events);
    }

    [Fact]
    public async Task EnrollAsync_WithTheSameSourceRecordTwice_ChangesNothing()
    {
        // Arrange
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync();
        await using var _ = harness;
        courses.AddCourse("course1");
        var first = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course1", "line-1")));

        // Act
        var second = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course1", "line-1")));

        // Assert
        Assert.True(second.Succeeded);
        Assert.False(second.Created);
        Assert.False(second.Changed);
        Assert.Equal(first.Enrollment.ItemId, second.Enrollment.ItemId);
        Assert.Single((await FindAsync(harness, first.Enrollment.ItemId)).Grants);
        Assert.Equal(["Enrolled"], observer.Events);
    }

    [Fact]
    public async Task EnrollAsync_WithAnotherSource_AddsAGrantToTheOpenEnrollment()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        var ends = LearningTestHarness.Now.AddDays(30);
        var first = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course1", "m1", ends)));

        // Act
        var second = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = "user1",
            CourseId = "course1",
            Source = "Order",
            SourceId = "line-9",
            AccessEndsUtc = ends.AddDays(30),
        }));

        // Assert
        Assert.False(second.Created);
        Assert.True(second.Changed);

        var enrollment = await FindAsync(harness, first.Enrollment.ItemId);

        Assert.Equal(2, enrollment.Grants.Count);
        Assert.Equal(ends.AddDays(30), enrollment.AccessEndsUtc);
    }

    [Fact]
    public async Task EnrollAsync_NeedingApproval_IsPendingUntilApproved()
    {
        // Arrange
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync();
        await using var _ = harness;
        courses.AddCourse("course1");
        var request = Manual("user1", "course1", "self-1");
        request.RequiresApproval = true;

        // Act
        var pending = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(request));
        var approved = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ApproveAsync(pending.Enrollment.ItemId, "manager"));

        // Assert
        Assert.Equal(EnrollmentStatus.Pending, pending.Enrollment.Status);
        Assert.True(approved.Succeeded);
        Assert.Equal(EnrollmentStatus.Active, (await FindAsync(harness, pending.Enrollment.ItemId)).Status);
        Assert.Equal(["Requested", "Status:Pending->Active", "Enrolled"], observer.Events);
    }

    [Fact]
    public async Task RejectAsync_WithdrawsThePendingEnrollmentWithTheReason()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        var request = Manual("user1", "course1", "self-1");
        request.RequiresApproval = true;
        var pending = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(request));

        // Act
        var rejected = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().RejectAsync(pending.Enrollment.ItemId, "manager", "Not in the target group"));

        // Assert
        Assert.True(rejected.Succeeded);

        var enrollment = await FindAsync(harness, pending.Enrollment.ItemId);

        Assert.Equal(EnrollmentStatus.Withdrawn, enrollment.Status);

        var change = Assert.Single(enrollment.History);

        Assert.Equal("manager", change.Actor);
        Assert.Equal("Not in the target group", change.Reason);
    }

    [Fact]
    public async Task EnrollAsync_AfterACompletedPass_StartsTheNextCycleLinkedToTheLastOne()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        var first = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course1", "m1")));
        await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(first.Enrollment.ItemId, EnrollmentStatus.Completed, "admin", "Completed in class"));

        // Act
        var second = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course1", "m2")));

        // Assert
        Assert.True(second.Created);
        Assert.NotEqual(first.Enrollment.ItemId, second.Enrollment.ItemId);
        Assert.Equal(2, second.Enrollment.Cycle);
        Assert.Equal(first.Enrollment.ItemId, second.Enrollment.PreviousEnrollmentId);
        Assert.Equal(EnrollmentStatus.Completed, (await FindAsync(harness, first.Enrollment.ItemId)).Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToCompletedByHand_NeedsAReason_AndKeepsASnapshot()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", title: "Safety basics");
        courses.AddItem("course1", "lesson1");
        var enrolled = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course1", "m1")));

        // Act
        var refused = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(enrolled.Enrollment.ItemId, EnrollmentStatus.Completed, "admin", " "));
        var completed = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(enrolled.Enrollment.ItemId, EnrollmentStatus.Completed, "admin", "Attended the live session"));

        // Assert
        Assert.Equal(EnrollmentErrors.ReasonRequired, refused.ErrorCode);
        Assert.True(completed.Succeeded);

        var enrollment = await FindAsync(harness, enrolled.Enrollment.ItemId);

        Assert.Equal(EnrollmentStatus.Completed, enrollment.Status);
        Assert.Equal(LearningTestHarness.Now, enrollment.CompletedUtc);
        Assert.Equal("Safety basics", enrollment.Completion.CourseTitle);
        Assert.Equal("admin", enrollment.Completion.CompletedBy);
        Assert.Equal("Attended the live session", enrollment.Completion.Reason);
        Assert.Equal("lesson1", Assert.Single(enrollment.Completion.Items).ContentItemId);
    }

    [Fact]
    public async Task ChangeStatusAsync_FromACompletedEnrollment_IsRefused()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        var enrolled = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course1", "m1")));
        await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(enrolled.Enrollment.ItemId, EnrollmentStatus.Completed, "admin", "Done"));

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(enrolled.Enrollment.ItemId, EnrollmentStatus.Active, "admin", "Oops"));

        // Assert
        Assert.Equal(EnrollmentErrors.InvalidTransition, result.ErrorCode);
    }

    [Fact]
    public async Task RevokeGrantAsync_WithdrawsAnOpenEnrollmentLeftWithNoGrant()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddCourse("course2");
        var paid = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest { UserId = "user1", CourseId = "course1", Source = "Order", SourceId = "line-1" }));
        var both = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest { UserId = "user1", CourseId = "course2", Source = "Order", SourceId = "line-1" }));
        await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course2", "m1")));

        // Act
        var changed = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().RevokeGrantAsync("Order", "line-1", "Orders", "Refunded"));

        // Assert
        Assert.Equal(2, changed.Count);
        Assert.Equal(EnrollmentStatus.Withdrawn, (await FindAsync(harness, paid.Enrollment.ItemId)).Status);

        var kept = await FindAsync(harness, both.Enrollment.ItemId);

        Assert.Equal(EnrollmentStatus.Active, kept.Status);
        Assert.Equal("Manual", Assert.Single(kept.Grants).Source);
    }

    [Fact]
    public async Task ExpireEndedAsync_ExpiresOpenEnrollmentsWhoseAccessEnded()
    {
        // Arrange
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync();
        await using var _ = harness;
        courses.AddCourse("course1");
        var ended = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course1", "m1", LearningTestHarness.Now.AddMinutes(-1))));
        var running = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user2", "course1", "m2", LearningTestHarness.Now.AddDays(1))));

        // Act
        var count = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ExpireEndedAsync());

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(EnrollmentStatus.Expired, (await FindAsync(harness, ended.Enrollment.ItemId)).Status);
        Assert.Equal(EnrollmentStatus.Active, (await FindAsync(harness, running.Enrollment.ItemId)).Status);
        Assert.Contains("Status:Active->Expired", observer.Events);
    }

    [Fact]
    public async Task ExtendAsync_OnAnExpiredEnrollment_GivesAccessBackAndMakesItActive()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        var enrolled = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "course1", "m1", LearningTestHarness.Now.AddMinutes(-1))));
        await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ExpireEndedAsync());

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ExtendAsync(enrolled.Enrollment.ItemId, LearningTestHarness.Now.AddDays(14), LearningTestHarness.Now.AddDays(10), "admin", "Was on leave"));

        // Assert
        Assert.True(result.Succeeded);

        var enrollment = await FindAsync(harness, enrolled.Enrollment.ItemId);

        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
        Assert.Equal(LearningTestHarness.Now.AddDays(14), enrollment.AccessEndsUtc);
        Assert.Equal(LearningTestHarness.Now.AddDays(10), enrollment.DueUtc);
    }

    [Fact]
    public async Task EnrollAsync_InACourseThatDoesNotExist_Fails()
    {
        // Arrange
        var (harness, _, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "missing", "m1")));

        // Assert
        Assert.Equal(EnrollmentErrors.CourseNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task EnrollAsync_WithoutACompletedPrerequisiteCourse_Fails_UnlessTheCallerSkipsPrerequisites()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("basics");
        courses.AddCourse("advanced", part => part.Prerequisites = [new CoursePrerequisite { Kind = "Course", Id = "basics" }]);

        // Act
        var refused = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(Manual("user1", "advanced", "m1")));
        var skipping = Manual("user1", "advanced", "m1");
        skipping.IgnorePrerequisites = true;
        var allowed = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(skipping));

        // Assert
        Assert.Equal(EnrollmentErrors.PrerequisitesNotMet, refused.ErrorCode);
        Assert.True(allowed.Succeeded);
    }

    [Fact]
    public async Task SelfEnrollment_FollowsTheCourseEnrollmentMode()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("open", part => part.EnrollmentMode = EnrollmentMode.Open);
        courses.AddCourse("keyed", part => part.EnrollmentMode = EnrollmentMode.Key);
        courses.AddCourse("approval", part => part.EnrollmentMode = EnrollmentMode.Approval);
        courses.AddCourse("closed", part => part.EnrollmentMode = EnrollmentMode.Closed);
        await harness.RunAsync(services => services.GetRequiredService<LearningSecretStore>().SetAsync(LearningSecretKinds.EnrollmentKey, "keyed", "K-123"));

        // Act
        Task<EnrollmentResult> Self(string course, string key = null)
            => harness.RunAsync(services => services.GetRequiredService<ISelfEnrollmentService>().EnrollAsync("user1", course, key));

        var open = await Self("open");
        var wrongKey = await Self("keyed", "nope");
        var rightKey = await Self("keyed", "K-123");
        var approval = await Self("approval");
        var closed = await Self("closed");

        // Assert
        Assert.Equal(EnrollmentStatus.Active, open.Enrollment.Status);
        Assert.Equal(LearningConstants.EnrollmentSources.Self, Assert.Single(open.Enrollment.Grants).Source);
        Assert.Equal(EnrollmentErrors.InvalidKey, wrongKey.ErrorCode);
        Assert.Equal(LearningConstants.EnrollmentSources.Key, Assert.Single(rightKey.Enrollment.Grants).Source);
        Assert.Equal(EnrollmentStatus.Pending, approval.Enrollment.Status);
        Assert.Equal(EnrollmentErrors.Closed, closed.ErrorCode);
    }

    [Fact]
    public async Task SelfEnrollment_LocksOutAfterFiveWrongKeys_ForFifteenMinutes()
    {
        // Arrange
        var clock = new LearningTestClock();
        var (harness, courses, _) = await LearningTestHarness.CreateAsync(services =>
        {
            clock.Use(services);
            services.AddDistributedMemoryCache();
        });
        await using var __ = harness;
        courses.AddCourse("keyed", part => part.EnrollmentMode = EnrollmentMode.Key);
        await harness.RunAsync(services => services.GetRequiredService<LearningSecretStore>().SetAsync(LearningSecretKinds.EnrollmentKey, "keyed", "K-123456"));

        Task<EnrollmentResult> Self(string key)
            => harness.RunAsync(services => services.GetRequiredService<ISelfEnrollmentService>().EnrollAsync("guesser", "keyed", key));

        // Act
        var wrong = new List<string>();

        for (var i = 0; i < 5; i++)
        {
            wrong.Add((await Self("guess-" + i)).ErrorCode);
        }

        var lockedOut = await Self("K-123456");
        clock.UtcNow = LearningTestHarness.Now.AddMinutes(16);
        var later = await Self("K-123456");

        // Assert
        Assert.All(wrong, code => Assert.Equal(EnrollmentErrors.InvalidKey, code));
        Assert.Equal(EnrollmentErrors.TooManyAttempts, lockedOut.ErrorCode);
        Assert.True(later.Succeeded, later.ErrorCode);
    }

    [Fact]
    public async Task TheEnrollmentKey_IsKeptOutsideTheCourseContentItem()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        var course = courses.AddCourse("keyed", part => part.EnrollmentMode = EnrollmentMode.Key);

        // Act
        await harness.RunAsync(services => services.GetRequiredService<LearningSecretStore>().SetAsync(LearningSecretKinds.EnrollmentKey, "keyed", "K-123456"));
        var hasKey = await harness.RunAsync(services => services.GetRequiredService<LearningSecretStore>().HasAsync(LearningSecretKinds.EnrollmentKey, "keyed"), commit: false);

        // Assert
        Assert.True(hasKey);
        Assert.DoesNotContain("KeyHash", System.Text.Json.JsonSerializer.Serialize(course.Content), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("v1:", System.Text.Json.JsonSerializer.Serialize(course.Content), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelfEnrollment_GivesAccessForTheCourseAccessDays()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("open", part =>
        {
            part.EnrollmentMode = EnrollmentMode.Open;
            part.AccessDays = 90;
        });

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<ISelfEnrollmentService>().EnrollAsync("user1", "open", null));

        // Assert
        Assert.Equal(LearningTestHarness.Now.AddDays(90), result.Enrollment.AccessEndsUtc);
    }

    private static EnrollmentRequest Manual(string userId, string courseId, string sourceId, DateTime? accessEndsUtc = null)
        => new()
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = sourceId,
            AccessEndsUtc = accessEndsUtc,
            Actor = "admin",
        };

    private static Task<Enrollment> FindAsync(Calendars.CalendarTestHarness harness, string id)
        => harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(id), commit: false);
}
