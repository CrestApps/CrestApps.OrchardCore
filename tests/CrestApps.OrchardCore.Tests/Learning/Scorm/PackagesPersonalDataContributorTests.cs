using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The export carries a person's runtime state on each package; erasure deletes it unless its enrollment is completed,
/// the rule quiz attempts and coursework submissions follow, and strips what identifies the learner from the one it keeps.
/// </summary>
public sealed class PackagesPersonalDataContributorTests
{
    [Fact]
    public async Task Export_ListsTheRuntimeStates()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");
        await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.core.lesson_status", "incomplete"), ("cmi.suspend_data", "page=9"), ("cmi.core.score.raw", "55")]);

        var export = new PersonalDataExport();
        await h.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<PackagesPersonalDataContributor>(services).ExportAsync(new PersonalDataExportContext(new PersonalDataSubject(userId: "ana", email: null), export));

            return true;
        });

        var record = Assert.Single(export.Records, record => record.Kind == PackagesPersonalDataContributor.Kind);

        Assert.Equal("incomplete", record.Fields["LessonStatus"]);
        Assert.Equal("55", record.Fields["Score"]);
        Assert.Equal("page=9", record.Fields["SuspendData"]);
        Assert.Equal("1", record.Fields["Sessions"]);
    }

    [Fact]
    public async Task Erasure_DeletesTheRuntimeStateOfAnEnrollmentThatIsNotCompleted()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        await h.LaunchAsync("ana");

        var result = await AnonymizeAsync(h, "ana");

        Assert.Null(await h.StateAsync(enrollment.ItemId));
        Assert.Equal(1, result.Records[PackagesPersonalDataContributor.Kind]);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Erasure_KeepsTheResultOfACompletedEnrollment_WithoutWhatIdentifiesTheLearner()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");
        await h.ApplyAsync("ana", launch, finish: true, changes:
        [
            ("cmi.core.lesson_status", "passed"),
            ("cmi.core.score.raw", "92"),
            ("cmi.core.session_time", "00:20:00"),
            ("cmi.core.lesson_location", "end"),
            ("cmi.suspend_data", "answers=abc"),
            ("cmi.comments", "I found this hard"),
            ("cmi.interactions.0.id", "q1"),
            ("cmi.interactions.0.student_response", "b"),
        ]);
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.Status = EnrollmentStatus.Completed);

        var result = await AnonymizeAsync(h, "ana");
        var kept = await h.StateAsync(enrollment.ItemId);

        Assert.Equal("passed", kept.Cmi.LessonStatus);
        Assert.Equal("92", kept.Cmi.ScoreRaw);
        Assert.Equal("0000:20:00.00", kept.Cmi.TotalTime);
        Assert.Equal(string.Empty, kept.Cmi.StudentId);
        Assert.Equal(string.Empty, kept.Cmi.StudentName);
        Assert.Equal(string.Empty, kept.Cmi.LessonLocation);
        Assert.Equal(string.Empty, kept.Cmi.SuspendData);
        Assert.Equal(string.Empty, kept.Cmi.Comments);
        Assert.Empty(kept.Cmi.Interactions);
        Assert.Equal(1, result.Records[PackagesPersonalDataContributor.Kind]);
        Assert.Single(result.Warnings);
    }

    private static async Task<PersonalDataAnonymizeResult> AnonymizeAsync(PackageTestHarness h, string userId)
    {
        var result = new PersonalDataAnonymizeResult();

        await h.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<PackagesPersonalDataContributor>(services).AnonymizeAsync(new PersonalDataAnonymizeContext(new PersonalDataSubject(userId: userId, email: null), null, result));

            return true;
        });

        return result;
    }
}
