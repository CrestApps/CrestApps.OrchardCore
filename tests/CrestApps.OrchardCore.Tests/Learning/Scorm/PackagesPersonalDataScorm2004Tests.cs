using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The personal data of a SCORM 2004 runtime state: exported with its statuses, score, time and what the learner wrote;
/// on erasure of a completed enrollment, kept without what identifies the learner.
/// </summary>
public sealed class PackagesPersonalDataScorm2004Tests
{
    private const string Item = PackageTestHarness.Package2004;

    [Fact]
    public async Task Export_ListsTheScorm2004State()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync(completionThreshold: null, scaledPassingScore: null);
        await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana", Item);
        await h.ApplyAsync("ana", launch, finish: true, changes:
        [
            ("cmi.completion_status", "completed"),
            ("cmi.success_status", "passed"),
            ("cmi.score.scaled", "0.8"),
            ("cmi.location", "slide-4"),
            ("cmi.suspend_data", "page=4"),
            ("cmi.session_time", "PT12M"),
            ("cmi.comments_from_learner.0.comment", "Clear"),
            ("cmi.interactions.0.id", "q1"),
            ("cmi.interactions.0.type", "choice"),
            ("cmi.interactions.0.learner_response", "b"),
            ("cmi.interactions.0.result", "correct"),
        ]);

        var export = new PersonalDataExport();
        await h.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<PackagesPersonalDataContributor>(services).ExportAsync(new PersonalDataExportContext(new PersonalDataSubject(userId: "ana", email: null), export));

            return true;
        });

        var record = Assert.Single(export.Records, record => record.Kind == PackagesPersonalDataContributor.Kind);

        Assert.Equal("SCORM 2004 4th Edition", record.Fields["Standard"]);
        Assert.Equal("completed", record.Fields["CompletionStatus"]);
        Assert.Equal("passed", record.Fields["SuccessStatus"]);
        Assert.Equal("80", record.Fields["Score"]);
        Assert.Equal("PT0H12M0S", record.Fields["TotalTime"]);
        Assert.Equal("slide-4", record.Fields["Location"]);
        Assert.Equal("page=4", record.Fields["SuspendData"]);
        Assert.Equal("Clear", record.Fields["Comments"]);
        Assert.Equal("q1: b (correct)", record.Fields["Interactions"]);
        Assert.Equal("1", record.Fields["Attempts"]);
    }

    [Fact]
    public async Task Erasure_KeepsTheScorm2004ResultOfACompletedEnrollment_WithoutWhatIdentifiesTheLearner()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync(completionThreshold: null, scaledPassingScore: null);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana", Item);
        await h.ApplyAsync("ana", launch, finish: true, changes:
        [
            ("cmi.completion_status", "completed"),
            ("cmi.score.scaled", "0.9"),
            ("cmi.location", "end"),
            ("cmi.suspend_data", "answers=abc"),
            ("cmi.learner_preference.language", "fr"),
            ("cmi.comments_from_learner.0.comment", "I found this hard"),
            ("cmi.interactions.0.id", "q1"),
        ]);
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.Status = EnrollmentStatus.Completed);

        var result = new PersonalDataAnonymizeResult();
        await h.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<PackagesPersonalDataContributor>(services).AnonymizeAsync(new PersonalDataAnonymizeContext(new PersonalDataSubject(userId: "ana", email: null), null, result));

            return true;
        });

        var kept = (await h.StateAsync(enrollment.ItemId, Item)).Cmi2004;

        Assert.Equal("completed", kept.CompletionStatus);
        Assert.Equal("0.9", kept.ScoreScaled);
        Assert.Equal(string.Empty, kept.LearnerId);
        Assert.Equal(string.Empty, kept.LearnerName);
        Assert.Null(kept.Location);
        Assert.Null(kept.SuspendData);
        Assert.Equal(string.Empty, kept.Language);
        Assert.Empty(kept.CommentsFromLearner);
        Assert.Empty(kept.Interactions);
        Assert.Single(result.Warnings);
    }
}
