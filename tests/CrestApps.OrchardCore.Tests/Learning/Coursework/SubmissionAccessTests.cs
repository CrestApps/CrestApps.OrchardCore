using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Review round 8, 7.3: a learner hands work in only while the course gives them access and once the item's release
/// rules open it, as for any other item of the course; a post to the submit action for a locked item or after the access
/// window ended is refused and keeps no file.
/// </summary>
public sealed class SubmissionAccessTests
{
    private const string Course = "course1";
    private const string Work = "essay1";

    [Fact]
    public async Task Submit_AfterTheAccessWindowEnded_IsRefused_AndKeepsNoFile()
    {
        // Arrange
        await using var h = await CreateAsync();
        var ana = await h.EnrollAsync("ana", Course);
        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var enrollment = await store.FindByIdAsync(ana.ItemId);

            enrollment.AccessEndsUtc = h.Clock.UtcNow.AddMinutes(-1);
            await store.SaveAsync(enrollment);

            return true;
        });

        // Act
        var result = await h.SubmitAsync("ana", Work, "Answer", ("essay.pdf", 100));

        // Assert
        Assert.Equal(SubmissionErrors.AccessEnded, result.Error);
        Assert.Empty(h.Files.Stored);
    }

    [Fact]
    public async Task Submit_OfAnItemAReleaseRuleKeepsLocked_IsRefused_UntilTheRuleOpensIt()
    {
        // Arrange: the coursework opens once the lesson before it is completed.
        await using var h = await CreateAsync(part => part.ReleaseRules =
        [
            new LearningRuleSettings { Name = LearningRuleNames.ItemCompleted, Values = { ["ItemId"] = "lesson1" } },
        ]);
        var ana = await h.EnrollAsync("ana", Course);

        // Act
        var locked = await h.SubmitAsync("ana", Work, "Answer", ("essay.pdf", 100));
        await h.RunAsync(services => services.GetRequiredService<CrestApps.OrchardCore.Learning.Services.ILearnerProgressService>().RecordAccessAsync(ana, "lesson1", 0));
        var open = await h.SubmitAsync("ana", Work, "Answer");

        // Assert
        Assert.Equal(SubmissionErrors.NotAvailableYet, locked.Error);
        Assert.Empty(h.Files.Stored);
        Assert.True(open.Succeeded, open.Error);
    }

    [Fact]
    public async Task Submit_BeforeADripDate_IsRefused()
    {
        // Arrange: the coursework opens a week after enrollment.
        await using var h = await CreateAsync(part => part.ReleaseRules =
        [
            new LearningRuleSettings { Name = LearningRuleNames.DaysAfterEnrollment, Values = { ["Days"] = "7" } },
        ]);
        await h.EnrollAsync("ana", Course);

        // Act
        var early = await h.SubmitAsync("ana", Work, "Answer");
        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(8);
        var later = await h.SubmitAsync("ana", Work, "Answer");

        // Assert
        Assert.Equal(SubmissionErrors.NotAvailableYet, early.Error);
        Assert.True(later.Succeeded, later.Error);
    }

    [Fact]
    public async Task Round11C1_Submit_BeforeTheCourseStarts_IsRefused()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        h.Courses.Get(Course).Alter<CoursePart>(part => part.StartsUtc = h.Clock.UtcNow.AddDays(3));

        // Act
        var early = await h.SubmitAsync("ana", Work, "Answer");

        // Assert
        Assert.Equal(SubmissionErrors.NotAvailableYet, early.Error);
    }

    private static async Task<CourseworkTestHarness> CreateAsync(Action<CourseItemPart> release = null)
    {
        var h = await CourseworkTestHarness.CreateAsync();

        h.Courses.AddCourse(Course);
        h.Courses.AddItem(Course, "lesson1");
        h.Courses.AddItem(Course, "lesson2");
        h.AddCoursework(Course, Work, part =>
        {
            part.SubmissionType = CourseworkSubmissionType.TextAndFiles;
            part.AllowedExtensions = [".pdf"];
            part.MaxFileSizeMb = 2;
            part.MaxFiles = 2;
            part.MaxPoints = 100;
        });

        if (release is not null)
        {
            h.Courses.Get(Work).Alter<CourseItemPart>(release);
        }

        return h;
    }
}
