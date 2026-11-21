using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Learning plan Phase 7 and section 9.2: the export carries a person's submissions with their file names; erasure
/// deletes the files and clears the text and feedback, but keeps the grade the course grade rests on.
/// </summary>
public sealed class CourseworkPersonalDataContributorTests
{
    [Fact]
    public async Task TheExport_ListsTheFiles_AndErasureDeletesThem_KeepingTheGrade()
    {
        // Arrange
        await using var h = await CourseworkTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.AddCoursework("course1", "work1", part => part.AllowedExtensions = [".pdf"]);
        await h.EnrollAsync("ana", "course1");
        var submitted = await h.SubmitAsync("ana", "work1", "My answer", ("essay.pdf", 100));
        await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 70, feedback: "Clear argument");

        // Act
        var export = new PersonalDataExport();
        var result = new PersonalDataAnonymizeResult();
        await h.RunAsync(async services =>
        {
            var contributor = ActivatorUtilities.CreateInstance<CourseworkPersonalDataContributor>(services);
            var subject = new PersonalDataSubject(userId: "ana", email: null);

            await contributor.ExportAsync(new PersonalDataExportContext(subject, export));
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result));

            return true;
        });
        var stored = await h.FindAsync(submitted.Submission.ItemId);

        // Assert
        var record = Assert.Single(export.Records, record => record.Kind == CourseworkPersonalDataContributor.Kind);
        Assert.Equal("essay.pdf", record.Fields["Files"]);
        Assert.Equal("My answer", record.Fields["Text"]);
        Assert.Empty(h.Files.Stored);
        Assert.Empty(stored.Files);
        Assert.Null(stored.Text);
        Assert.Null(stored.Feedback);
        Assert.Equal(70m, stored.Score);
        Assert.Equal(SubmissionStatus.Graded, stored.Status);
        Assert.Equal(1, result.Records[CourseworkPersonalDataContributor.Kind]);
    }

    [Fact]
    public async Task Erasure_DeletesTheSubmissionsAndFiles_OfAnEnrollmentThatIsNotCompleted()
    {
        // Arrange: a lesson nobody opens keeps the course open, so the enrollment is not completed.
        await using var h = await CourseworkTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Courses.AddItem("course1", "lesson1");
        h.AddCoursework("course1", "work1", part => part.AllowedExtensions = [".pdf"]);
        var ana = await h.EnrollAsync("ana", "course1");
        var graded = await h.SubmitAsync("ana", "work1", "My answer", ("essay.pdf", 100));
        await h.GradeAsync(graded.Submission.ItemId, "grader", points: 70);

        // Act
        var result = await AnonymizeAsync(h, "ana");
        var stored = await h.FindAsync(graded.Submission.ItemId);
        var waiting = await h.RunAsync(services => services.GetRequiredService<ISubmissionService>().ListWaitingAsync(null, 0, 10));

        // Assert
        Assert.Null(stored);
        Assert.Empty(h.Files.Stored);
        Assert.Equal(0, waiting.Count);
        Assert.Equal(1, result.Records[CourseworkPersonalDataContributor.Kind]);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Erasure_DeletesASubmissionWaitingForAGrader_WhoseEnrollmentIsGone()
    {
        // Arrange: Learning's own erasure deleted the enrollment that was not completed.
        await using var h = await CourseworkTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Courses.AddItem("course1", "lesson1");
        h.AddCoursework("course1", "work1", part => part.AllowedExtensions = [".pdf"]);
        var ana = await h.EnrollAsync("ana", "course1");
        var submitted = await h.SubmitAsync("ana", "work1", "My answer", ("essay.pdf", 100));
        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();

            await store.DeleteAsync(await store.FindByIdAsync(ana.ItemId));

            return true;
        });

        // Act
        await AnonymizeAsync(h, "ana");
        var stored = await h.FindAsync(submitted.Submission.ItemId);
        var waiting = await h.RunAsync(services => services.GetRequiredService<ISubmissionService>().ListWaitingAsync(null, 0, 10));

        // Assert: no empty row is left in the grading queue.
        Assert.Null(stored);
        Assert.Empty(h.Files.Stored);
        Assert.Equal(0, waiting.Count);
    }

    private static async Task<PersonalDataAnonymizeResult> AnonymizeAsync(CourseworkTestHarness h, string userId)
    {
        var result = new PersonalDataAnonymizeResult();

        await h.RunAsync(async services =>
        {
            var contributor = ActivatorUtilities.CreateInstance<CourseworkPersonalDataContributor>(services);

            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(new PersonalDataSubject(userId: userId, email: null), null, result));

            return true;
        });

        return result;
    }
}
