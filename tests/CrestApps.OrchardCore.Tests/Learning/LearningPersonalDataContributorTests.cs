using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// A person's enrollments and progress are exported; erasure deletes open enrollments with their progress and keeps a
/// completed enrollment, which proves training, with a warning.
/// </summary>
public sealed class LearningPersonalDataContributorTests
{
    [Fact]
    public async Task Export_ListsEnrollmentsAndProgress_AndAnonymize_KeepsOnlyCompletedEnrollments()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync(services => services.AddSingleton(UserManagerWith().Object));
        await using var _harness = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson");
        courses.AddItem("course1", "lesson2");
        courses.AddCourse("course2");
        courses.AddItem("course2", "task", completionMode: CompletionModes.MarkedDone);
        var open = await EnrollAsync(harness, "user-1", "course1");
        var done = await EnrollAsync(harness, "user-1", "course2");
        var other = await EnrollAsync(harness, "user-2", "course1");
        await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(open, "lesson", 0));
        await harness.RunAsync(async services =>
        {
            var enrollment = await services.GetRequiredService<EnrollmentStore>().FindByIdAsync(done.ItemId);

            return await services.GetRequiredService<ILearnerProgressService>().MarkDoneAsync(enrollment, "task");
        });

        // Act
        var export = new PersonalDataExport();
        var result = new PersonalDataAnonymizeResult();
        await harness.RunAsync(async services =>
        {
            var contributor = ActivatorUtilities.CreateInstance<LearningPersonalDataContributor>(services);
            var subject = new PersonalDataSubject(userId: "user-1", email: null);

            await contributor.ExportAsync(new PersonalDataExportContext(subject, export));
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result));
        });

        // Assert
        Assert.Equal(new[] { done.ItemId, open.ItemId }.Order(), export.Records.Where(record => record.Kind == "LearningEnrollment").Select(record => record.Id).Order());
        Assert.Contains(export.Records, record => record.Kind == "LearningProgress" && record.Id == open.ItemId + ":lesson");
        Assert.Equal(1, result.Records["LearningEnrollment"]);
        Assert.Equal(1, result.Records["LearningProgress"]);
        Assert.Single(result.Warnings);

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();

            Assert.Null(await store.FindByIdAsync(open.ItemId));
            Assert.Equal(EnrollmentStatus.Completed, (await store.FindByIdAsync(done.ItemId)).Status);
            Assert.NotNull(await store.FindByIdAsync(other.ItemId));
            Assert.Null(await services.GetRequiredService<EnrollmentProgressStore>().FindByEnrollmentAsync(open.ItemId));
            Assert.NotNull(await services.GetRequiredService<EnrollmentProgressStore>().FindByEnrollmentAsync(done.ItemId));
        });
    }

    [Fact]
    public async Task AnErasureByEmail_ReachesTheLearningRecordsOfTheAccountWithThatEmail()
    {
        // Arrange: a request made from a customer added by hand knows the learner's e-mail only, not their account.
        var (harness, courses, _) = await LearningTestHarness.CreateAsync(services => services.AddSingleton(UserManagerWith(("learner@example.com", "user-1")).Object));
        await using var _harness = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson");
        var open = await EnrollAsync(harness, "user-1", "course1");
        var other = await EnrollAsync(harness, "user-2", "course1");
        var subject = new PersonalDataSubject(userId: null, email: "Learner@Example.com");
        var result = new PersonalDataAnonymizeResult();

        // Act
        await harness.RunAsync(async services =>
        {
            var contributor = ActivatorUtilities.CreateInstance<LearningPersonalDataContributor>(services);

            await ((IPersonalDataContributor)contributor).IdentifyAsync(subject);
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result));
        });

        // Assert
        Assert.Contains("user-1", subject.UserIds);
        Assert.Equal(1, result.Records["LearningEnrollment"]);

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();

            Assert.Null(await store.FindByIdAsync(open.ItemId));
            Assert.NotNull(await store.FindByIdAsync(other.ItemId));
        });
    }

    private static Mock<UserManager<IUser>> UserManagerWith(params (string Email, string UserId)[] accounts)
    {
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);

        foreach (var (email, userId) in accounts)
        {
            var user = Mock.Of<IUser>();

            userManager.Setup(manager => manager.FindByEmailAsync(It.Is<string>(value => string.Equals(value, email, StringComparison.OrdinalIgnoreCase)))).ReturnsAsync(user);
            userManager.Setup(manager => manager.GetUserIdAsync(user)).ReturnsAsync(userId);
        }

        return userManager;
    }

    private static async Task<Enrollment> EnrollAsync(CrestApps.OrchardCore.Tests.Calendars.CalendarTestHarness harness, string userId, string courseId)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId + courseId,
        }));

        return result.Enrollment;
    }
}
