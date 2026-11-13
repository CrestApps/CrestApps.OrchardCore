using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class LearningUpkeepTests
{
    [Fact]
    public async Task RunAsync_ExpiresEndedAccess_RaisesAccessEndingOnce_AndInactiveLearnersOnce()
    {
        // Arrange
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync();
        await using var _ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson");
        var ended = await EnrollAsync(harness, "ended", LearningTestHarness.Now.AddHours(-1));
        var ending = await EnrollAsync(harness, "ending", LearningTestHarness.Now.AddDays(3));
        var later = await EnrollAsync(harness, "later", LearningTestHarness.Now.AddDays(30));
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var enrollment = await store.FindByIdAsync(later.ItemId);

            enrollment.CreatedUtc = LearningTestHarness.Now.AddDays(-30);
            enrollment.LastActivityUtc = LearningTestHarness.Now.AddDays(-20);
            await store.SaveAsync(enrollment);
        });

        // Act
        await harness.RunAsync(services => services.GetRequiredService<LearningUpkeep>().RunAsync());
        await harness.RunAsync(services => services.GetRequiredService<LearningUpkeep>().RunAsync());

        // Assert
        Assert.Equal(EnrollmentStatus.Expired, (await FindAsync(harness, ended.ItemId)).Status);
        Assert.Single(observer.Events, name => name == "Status:Active->Expired");
        Assert.Single(observer.Events, name => name == "AccessEnding:" + ending.ItemId);
        Assert.Single(observer.Events, name => name == "Inactive:" + later.ItemId);
        Assert.DoesNotContain(observer.Events, name => name == "Inactive:" + ending.ItemId);
    }

    private static async Task<Enrollment> EnrollAsync(CalendarTestHarness harness, string userId, DateTime accessEndsUtc)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = "course1",
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
            AccessEndsUtc = accessEndsUtc,
        }));

        return result.Enrollment;
    }

    private static Task<Enrollment> FindAsync(CalendarTestHarness harness, string id)
        => harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(id), commit: false);
}
