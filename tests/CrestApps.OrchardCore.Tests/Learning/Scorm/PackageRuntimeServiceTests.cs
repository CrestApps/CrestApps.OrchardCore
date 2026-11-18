using System.Text;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class PackageRuntimeServiceTests
{
    [Fact]
    public async Task FirstLaunch_StartsAnAbInitioSession_WithTheLearnersReadOnlyValues()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");

        var launch = await h.LaunchAsync("ana");

        Assert.True(launch.Succeeded, launch.Error);
        Assert.False(launch.Review);
        Assert.Equal(new PackageLaunch(launch.Launch.LaunchId, enrollment.ItemId, PackageTestHarness.Package, launch.Part.PackageFileId, "ana"), launch.Launch);

        var state = await h.StateAsync(enrollment.ItemId);

        Assert.Equal(1, state.Sessions);
        Assert.Equal(launch.Launch.LaunchId, state.CurrentLaunchId);
        Assert.Equal("ana", state.UserId);
        Assert.Equal(PackageTestHarness.Course, state.CourseId);
        Assert.Equal("ab-initio", state.Cmi.Entry);
        Assert.Equal("ana", state.Cmi.StudentId);
        Assert.Equal("ana name", state.Cmi.StudentName);
        Assert.Equal("credit", state.Cmi.Credit);
        Assert.Equal("normal", state.Cmi.LessonMode);
        Assert.Equal("80", state.Cmi.MasteryScore);
        Assert.Equal("level=2", state.Cmi.LaunchData);
        Assert.Equal(h.Clock.UtcNow, state.LastLaunchedUtc);
    }

    [Fact]
    public async Task LaunchAfterASuspendedSession_Resumes_WithTheSuspendData()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var first = await h.LaunchAsync("ana");

        var applied = await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.suspend_data", "page=4"), ("cmi.core.exit", "suspend"), ("cmi.core.lesson_status", "incomplete")]);
        Assert.True(applied.Succeeded, applied.Error);

        var second = await h.LaunchAsync("ana");
        var state = await h.StateAsync(enrollment.ItemId);

        Assert.True(second.Succeeded, second.Error);
        Assert.Equal(2, state.Sessions);
        Assert.Equal("resume", state.Cmi.Entry);
        Assert.Equal("page=4", state.Cmi.SuspendData);
        Assert.Equal(string.Empty, state.Cmi.Exit);
    }

    [Fact]
    public async Task Launch_WithoutAnEnrollment_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync();

        Assert.Equal(PackageRuntimeErrors.NotEnrolled, (await h.LaunchAsync("bo")).Error);
    }

    [Fact]
    public async Task Launch_AfterTheAccessWindowEnded_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.AccessEndsUtc = h.Clock.UtcNow.AddMinutes(-1));

        Assert.Equal(PackageRuntimeErrors.AccessEnded, (await h.LaunchAsync("ana")).Error);
        Assert.Null(await h.StateAsync(enrollment.ItemId));
    }

    [Fact]
    public async Task Launch_OfASuspendedEnrollment_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.Status = EnrollmentStatus.Suspended);

        Assert.Equal(PackageRuntimeErrors.NotActive, (await h.LaunchAsync("ana")).Error);
    }

    [Fact]
    public async Task Launch_OfAnItemAReleaseRuleKeepsLocked_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync(configureItem: part => part.ReleaseRules =
        [
            new LearningRuleSettings { Name = LearningRuleNames.ItemCompleted, Values = { ["ItemId"] = "intro" } },
        ]);
        await h.EnrollAsync("ana");

        Assert.Equal(PackageRuntimeErrors.NotAvailableYet, (await h.LaunchAsync("ana")).Error);
    }

    [Fact]
    public async Task Launch_OfAnItemWithoutAPackage_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        h.Courses.AddItem(PackageTestHarness.Course, "empty", kind: CrestApps.OrchardCore.Learning.Packages.PackagesConstants.PackageKind).Alter<LearningPackagePart>(_ => { });
        await h.EnrollAsync("ana");

        Assert.Equal(PackageRuntimeErrors.NoPackageFile, (await h.LaunchAsync("ana", "empty")).Error);
        Assert.Equal(PackageRuntimeErrors.NotAPackage, (await h.LaunchAsync("ana", "intro")).Error);
    }

    [Fact]
    public async Task Apply_ChecksEveryValueOnTheServer_AndKeepsOnlyTheValidOnes()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        var result = await h.ApplyAsync("ana", launch, commit: true, changes:
        [
            ("cmi.core.lesson_location", "slide-7"),
            ("cmi.core.lesson_status", "not attempted"),
            ("cmi.core.student_id", "someone-else"),
            ("cmi.core.score.raw", "150"),
            ("cmi.suspend_data", new string('s', 4097)),
            ("cmi.interactions.1.id", "q2"),
            ("cmi.core._children", "x"),
            ("cmi.interactions.0.id", "q1"),
            ("cmi.core.session_time", "00:05:00"),
        ]);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            [(1, Scorm12Errors.IncorrectDataType), (2, Scorm12Errors.ReadOnly), (3, Scorm12Errors.IncorrectDataType), (4, Scorm12Errors.IncorrectDataType), (5, Scorm12Errors.InvalidArgument), (6, Scorm12Errors.InvalidSetKeyword)],
            result.Refused.Select(refusal => (refusal.Index, refusal.Code)));

        var state = await h.StateAsync(enrollment.ItemId);

        Assert.Equal("slide-7", state.Cmi.LessonLocation);
        Assert.Equal("not attempted", state.Cmi.LessonStatus);
        Assert.Equal("ana", state.Cmi.StudentId);
        Assert.Equal(string.Empty, state.Cmi.ScoreRaw);
        Assert.Equal(string.Empty, state.Cmi.SuspendData);
        Assert.Equal("q1", Assert.Single(state.Cmi.Interactions).Id);
        Assert.Equal("00:05:00", state.Cmi.SessionTime);
        Assert.Equal(h.Clock.UtcNow, state.LastCommitUtc);
    }

    [Fact]
    public async Task Apply_WithTooManyValues_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        var changes = Enumerable.Range(0, PackageRuntimeService.MaxChangesPerRequest + 1).Select(_ => ("cmi.core.lesson_location", "x")).ToArray();

        Assert.Equal(PackageRuntimeErrors.TooManyChanges, (await h.ApplyAsync("ana", launch, changes: changes)).Error);
    }

    [Fact]
    public async Task Apply_WithTheTokenOfAnotherLearner_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var anaEnrollment = await h.EnrollAsync("ana");
        await h.EnrollAsync("bo");
        var anaLaunch = await h.LaunchAsync("ana");

        var result = await h.ApplyAsync("bo", anaLaunch, commit: true, changes: [("cmi.core.lesson_location", "bo was here")]);

        Assert.Equal(PackageRuntimeErrors.InvalidToken, result.Error);
        Assert.Equal(string.Empty, (await h.StateAsync(anaEnrollment.ItemId)).Cmi.LessonLocation);
    }

    [Fact]
    public async Task Apply_NamingAnotherEnrollmentThanTheToken_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var boEnrollment = await h.EnrollAsync("bo");
        await h.LaunchAsync("bo");
        var anaLaunch = await h.LaunchAsync("ana");

        var result = await h.ApplyAsync("ana", anaLaunch, commit: true, enrollmentId: boEnrollment.ItemId, changes: [("cmi.core.lesson_location", "x")]);

        Assert.Equal(PackageRuntimeErrors.InvalidToken, result.Error);
        Assert.Equal(string.Empty, (await h.StateAsync(boEnrollment.ItemId)).Cmi.LessonLocation);
    }

    [Fact]
    public async Task Apply_WithATamperedOrExpiredToken_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");
        var tampered = launch.Token[..^4] + (launch.Token[^4] == 'x' ? "yyyy" : "xxxx");

        Assert.Equal(PackageRuntimeErrors.InvalidToken, (await h.ApplyAsync("ana", launch, token: tampered, changes: [("cmi.core.lesson_location", "x")])).Error);

        h.Clock.UtcNow += PackageLaunchTokens.Lifetime + TimeSpan.FromMinutes(1);

        Assert.Equal(PackageRuntimeErrors.InvalidToken, (await h.ApplyAsync("ana", launch, changes: [("cmi.core.lesson_location", "x")])).Error);
    }

    [Fact]
    public async Task Apply_FromAnOlderLaunch_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var first = await h.LaunchAsync("ana");
        var second = await h.LaunchAsync("ana");

        Assert.Equal(PackageRuntimeErrors.Superseded, (await h.ApplyAsync("ana", first, changes: [("cmi.core.lesson_location", "x")])).Error);
        Assert.True((await h.ApplyAsync("ana", second, changes: [("cmi.core.lesson_location", "x")])).Succeeded);
    }

    [Fact]
    public async Task Finish_AddsTheSessionTime_AndEndsTheLaunch()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        await h.ApplyAsync("ana", launch, changes: [("cmi.core.session_time", "00:10:00"), ("cmi.core.lesson_status", "incomplete")]);
        var finished = await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.core.session_time", "00:12:30")]);

        Assert.True(finished.Succeeded, finished.Error);

        var state = await h.StateAsync(enrollment.ItemId);

        Assert.Equal("0000:12:30.00", state.Cmi.TotalTime);
        Assert.Equal(h.Clock.UtcNow, state.LastFinishedUtc);
        Assert.Null(state.CurrentLaunchId);
        Assert.Equal(PackageRuntimeErrors.Superseded, (await h.ApplyAsync("ana", launch, changes: [("cmi.core.lesson_location", "x")])).Error);
    }

    [Fact]
    public async Task Apply_AfterTheAccessWindowEnded_IsRefused()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.AccessEndsUtc = h.Clock.UtcNow.AddMinutes(-1));

        Assert.Equal(PackageRuntimeErrors.AccessEnded, (await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.core.lesson_status", "completed")])).Error);
        Assert.Equal("not attempted", (await h.StateAsync(enrollment.ItemId)).Cmi.LessonStatus);
    }

    [Fact]
    public async Task Apply_WhileAnotherWriteHoldsTheLock_IsBusy_AndChangesNothing()
    {
        var busy = new BusyLock("Learning:PackageRuntime:");
        await using var h = await PackageTestHarness.CreateAsync(distributedLock: busy);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        busy.RefuseNext(1);

        var result = await h.ApplyAsync("ana", launch, changes: [("cmi.core.lesson_location", "x")]);

        Assert.Equal(PackageRuntimeErrors.Busy, result.Error);
        Assert.Equal(1, busy.Refused);
        Assert.Equal(string.Empty, (await h.StateAsync(enrollment.ItemId)).Cmi.LessonLocation);
    }

    [Fact]
    public async Task LaunchAndApply_RunUnderOneLockPerEnrollmentAndPackage()
    {
        var locks = new RecordingLock();
        await using var h = await PackageTestHarness.CreateAsync(distributedLock: locks);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        await h.ApplyAsync("ana", launch, changes: [("cmi.core.lesson_location", "x")]);

        Assert.Equal(2, locks.Keys.Count(key => key == PackageRuntimeService.LockKey(enrollment.ItemId, PackageTestHarness.Package)));
    }

    [Fact]
    public async Task CompletedEnrollment_LaunchesForReview_AndKeepsNothing()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.Status = EnrollmentStatus.Completed);

        var launch = await h.LaunchAsync("ana");

        Assert.True(launch.Succeeded, launch.Error);
        Assert.True(launch.Review);
        Assert.Equal("review", launch.State.Cmi.LessonMode);
        Assert.Equal("no-credit", launch.State.Cmi.Credit);

        var applied = await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.core.lesson_status", "failed")]);

        Assert.True(applied.Succeeded, applied.Error);
        Assert.True(applied.Review);
        Assert.Equal("not attempted", (await h.StateAsync(enrollment.ItemId)).Cmi.LessonStatus);
    }

    [Fact]
    public async Task OpenFile_ServesThePackagesFilesForTheLaunch()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        await using (var page = await h.RunAsync(services => services.GetRequiredService<PackageRuntimeService>().OpenFileAsync(launch.Token, "index.html")))
        {
            Assert.True(page.IsHtml);
            Assert.Equal("index.html", page.Path);
            Assert.Equal(launch.Launch, page.Launch);
            Assert.Equal("ana", page.State.Cmi.StudentId);
            Assert.Contains("<title>Lesson</title>", await new StreamReader(page.Content, Encoding.UTF8).ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        await using (var script = await h.RunAsync(services => services.GetRequiredService<PackageRuntimeService>().OpenFileAsync(launch.Token, "scripts/lesson.js")))
        {
            Assert.False(script.IsHtml);
            Assert.Equal("text/javascript", script.ContentType);
            Assert.Equal("var lesson = 1;", await new StreamReader(script.Content).ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        await using (var otherCase = await h.RunAsync(services => services.GetRequiredService<PackageRuntimeService>().OpenFileAsync(launch.Token, "Scripts/Lesson.js")))
        {
            Assert.Equal("scripts/lesson.js", otherCase.Path);
        }
    }

    [Theory]
    [InlineData("../imsmanifest.xml")]
    [InlineData("scripts/../../index.html")]
    [InlineData("/index.html")]
    [InlineData("C:/index.html")]
    [InlineData("missing.html")]
    [InlineData("")]
    public async Task OpenFile_RefusesPathsOutsideThePackage(string path)
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        Assert.Null(await h.RunAsync(services => services.GetRequiredService<PackageRuntimeService>().OpenFileAsync(launch.Token, path)));
    }

    [Fact]
    public async Task OpenFile_RefusesAReplacedPackage_AWithdrawnEnrollment_AndAnAlteredToken()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        Assert.Null(await h.RunAsync(services => services.GetRequiredService<PackageRuntimeService>().OpenFileAsync(launch.Token[..^3] + "abc", "index.html")));

        h.Courses.Get(PackageTestHarness.Package).Alter<LearningPackagePart>(part => part.PackageFileId = "replaced");
        Assert.Null(await h.RunAsync(services => services.GetRequiredService<PackageRuntimeService>().OpenFileAsync(launch.Token, "index.html")));

        h.Courses.Get(PackageTestHarness.Package).Alter<LearningPackagePart>(part => part.PackageFileId = launch.Launch.PackageFileId);
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.Status = EnrollmentStatus.Withdrawn);
        Assert.Null(await h.RunAsync(services => services.GetRequiredService<PackageRuntimeService>().OpenFileAsync(launch.Token, "index.html")));
    }

    [Fact]
    public async Task Review_AfterASessionThatEndedNormally_ReportsNoEntry()
    {
        // SCORM 1.2 RTE cmi.core.entry: ab-initio the first time, resume after a suspended session, empty otherwise.
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var first = await h.LaunchAsync("ana");
        await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.core.lesson_status", "passed")]);
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.Status = EnrollmentStatus.Completed);

        var review = await h.LaunchAsync("ana");

        Assert.True(review.Review);
        Assert.Equal("ab-initio", first.State.Cmi.Entry);
        Assert.Equal(string.Empty, review.State.Cmi.Entry);
    }
}
