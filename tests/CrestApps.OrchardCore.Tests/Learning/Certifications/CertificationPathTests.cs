using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Certifications;
using CrestApps.OrchardCore.Learning.Certifications.Core;
using CrestApps.OrchardCore.Learning.Certifications.Core.Indexes;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Core;
using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning.Certifications;

/// <summary>
/// Certification paths end to end on the real Learning, Assessments and Skills services: a certificate earned from a
/// course and an exam, issued once however often the completion arrives, issued by the upkeep when the event is lost,
/// renewed in the window as a second cycle, lapsed at expiry and stopped by a revocation.
/// </summary>
public sealed class CertificationPathTests
{
    [Fact]
    public async Task CourseAndExam_IssueOneCertificate_WithEvidenceExpiryAndRenewalDate()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        var path = await h.AddPathAsync();

        // Act
        await h.CompleteCourseAsync("ana", "safety");
        var afterCourse = await h.ProgressAsync(path.ItemId, "ana");
        var recordsAfterCourse = await h.RecordsAsync("ana");

        await h.PassQuizAsync("ana");
        var progress = await h.ProgressAsync(path.ItemId, "ana");
        var record = Assert.Single(await h.RecordsAsync("ana"));
        var documents = await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().ListProgressForUserAsync("ana"));

        // Assert (one progress document: a commit in the middle must not make a second copy)
        Assert.Single(documents);
        Assert.Equal(CertificationPathState.InProgress, afterCourse.State);
        Assert.Single(afterCourse.CurrentCycle().Evidence);
        Assert.Empty(recordsAfterCourse);

        Assert.Equal(CertificationPathState.Certified, progress.State);
        Assert.Equal(record.ItemId, progress.CurrentRecordId);
        Assert.Equal(2, progress.CurrentCycle().Evidence.Count);
        Assert.Equal(record.ItemId, progress.CurrentCycle().RecordId);
        Assert.Equal(CertificationsConstants.Source, record.Source);
        Assert.Equal($"{progress.ItemId}:1", record.SourceId);
        Assert.Equal(CertificationStatus.Valid, record.Status);
        Assert.Equal(CertificationHarness.Today, record.IssuedOn);
        Assert.Equal(CertificationHarness.Today.AddMonths(12).AddDays(-1), record.ExpiresOn);
        Assert.Equal(record.ExpiresOn.Value.AddDays(-60), progress.RenewalOpensOn);
        Assert.Equal("Name of ana", record.SubjectName);
        Assert.NotNull(record.VerificationCode);
    }

    [Fact]
    public async Task CompletionDeliveredTwice_IssuesOneCertificate()
    {
        // Arrange (review round 2, D: a completion event delivered twice)
        await using var h = await CertificationHarness.CreateAsync();
        var path = await h.AddPathAsync(join: RequirementJoin.Any);

        await h.CompleteCourseAsync("ana", "safety");

        var enrollment = (await h.EnrollmentsAsync("ana", "safety")).Single();

        // Act
        await h.RunAsync(services => services.GetServices<IEnrollmentObserver>().OfType<CertificationEnrollmentObserver>().Single().CompletedAsync(enrollment));
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().EvaluateAsync(path.ItemId, "ana"))));

        // Assert
        Assert.Single(await h.RecordsAsync("ana"));
        Assert.Equal(CertificationPathState.Certified, (await h.ProgressAsync(path.ItemId, "ana")).State);
    }

    [Fact]
    public async Task CompletionEventLost_TheUpkeepIssuesTheCertificate()
    {
        // Arrange (review round 2, D: observers run after the commit and swallow their failures, so an event can be lost)
        await using var h = await CertificationHarness.CreateAsync(dropCompletionEvents: true);
        var path = await h.AddPathAsync(join: RequirementJoin.Any);

        await h.CompleteCourseAsync("ana", "safety");

        var before = await h.RecordsAsync("ana");

        // Act
        var issued = await h.RunAsync(services => services.GetRequiredService<CertificationUpkeep>().RunAsync());

        // Assert
        Assert.Empty(before);
        Assert.Equal(1, issued);
        Assert.Single(await h.RecordsAsync("ana"));
        Assert.Equal(CertificationPathState.Certified, (await h.ProgressAsync(path.ItemId, "ana")).State);
    }

    [Fact]
    public async Task Upkeep_DoesNotIssueForCompletionsBeforeThePathExisted()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync(dropCompletionEvents: true);

        await h.CompleteCourseAsync("ana", "safety");
        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(3);

        await h.AddPathAsync(join: RequirementJoin.Any);

        // Act
        var issued = await h.RunAsync(services => services.GetRequiredService<CertificationUpkeep>().RunAsync());

        // Assert
        Assert.Equal(0, issued);
        Assert.Empty(await h.RecordsAsync("ana"));
    }

    [Fact]
    public async Task Renewal_OpensInTheWindow_EnrollsAgain_AndIssuesALinkedCertificate_KeepingBothCycles()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        var path = await h.AddPathAsync(configure: path => path.RenewalRequirements = [new CertificationRequirement { Kind = CertificationsConstants.RequirementKinds.CourseCompleted, TargetId = "safety" }]);

        await h.CompleteCourseAsync("ana", "safety");
        await h.PassQuizAsync("ana");

        var first = await h.ProgressAsync(path.ItemId, "ana");
        var firstEnrollment = (await h.EnrollmentsAsync("ana", "safety")).Single();

        // Act: the day renewal opens
        h.Clock.UtcNow = first.RenewalOpensOn.Value.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        await h.RunAsync(services => services.GetRequiredService<CertificationUpkeep>().RunAsync());

        var opened = await h.ProgressAsync(path.ItemId, "ana");
        var renewalEnrollment = (await h.EnrollmentsAsync("ana", "safety")).Single(enrollment => enrollment.ItemId != firstEnrollment.ItemId);

        await h.CompleteEnrollmentAsync(renewalEnrollment, "safety");

        var renewed = await h.ProgressAsync(path.ItemId, "ana");
        var records = await h.RecordsAsync("ana");

        // Assert
        Assert.Equal(CertificationPathState.RenewalOpen, opened.State);
        Assert.Equal(2, opened.Cycle);
        Assert.Contains(renewalEnrollment.ItemId, opened.CurrentCycle().EnrollmentIds);
        Assert.Equal(firstEnrollment.ItemId, renewalEnrollment.PreviousEnrollmentId);

        Assert.Equal(CertificationPathState.Certified, renewed.State);
        Assert.Equal(2, renewed.Cycles.Count);
        Assert.Equal(first.CurrentRecordId, renewed.Cycles[0].RecordId);
        Assert.Equal(2, records.Count);

        var old = records.Single(record => record.ItemId == first.CurrentRecordId);
        var current = records.Single(record => record.ItemId == renewed.CurrentRecordId);

        Assert.Equal(current.ItemId, old.RenewedByRecordId);
        Assert.Equal(old.ItemId, current.PreviousRecordId);
        // The path counts from the completion date: the renewal runs twelve months from the day it was earned.
        Assert.Equal(DateOnly.FromDateTime(h.Clock.UtcNow).AddMonths(12).AddDays(-1), current.ExpiresOn);
        Assert.Equal(renewalEnrollment.ItemId, Assert.Single(renewed.CurrentCycle().Evidence).SourceId);
    }

    [Fact]
    public async Task Renewal_DoesNotCountTheCompletionOfTheEarlierCycle()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        var path = await h.AddPathAsync(join: RequirementJoin.Any);

        await h.CompleteCourseAsync("ana", "safety");

        var first = await h.ProgressAsync(path.ItemId, "ana");

        h.Clock.UtcNow = first.RenewalOpensOn.Value.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        await h.RunAsync(services => services.GetRequiredService<CertificationUpkeep>().RunAsync());

        // Act
        await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().EvaluateAsync(path.ItemId, "ana"));

        // Assert
        Assert.Equal(CertificationPathState.RenewalOpen, (await h.ProgressAsync(path.ItemId, "ana")).State);
        Assert.Single(await h.RecordsAsync("ana"));
    }

    [Fact]
    public async Task Expiry_LapsesTheProgress_AndARenewalInTheGracePeriodStillRenews()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        var path = await h.AddPathAsync(join: RequirementJoin.Any, configure: path =>
        {
            path.RenewalWindowDays = 0;
            path.GracePeriodDays = 30;
            path.EnrollOnRenewal = false;
        });

        await h.CompleteCourseAsync("ana", "safety");

        var first = await h.ProgressAsync(path.ItemId, "ana");

        // Act: the day after expiry, Skills' daily check expires the certificate
        var dayAfter = first.ExpiresOn.Value.AddDays(1);

        h.Clock.UtcNow = dayAfter.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc);
        await h.RunAsync(services => services.GetRequiredService<SkillsDailyCheck>().RunAsync(dayAfter));

        var lapsed = await h.ProgressAsync(path.ItemId, "ana");
        var enrollment = (await h.EnrollmentsAsync("ana", "safety")).Single();

        await h.ReEnrollAndCompleteAsync("ana", "safety", enrollment.ItemId);

        var renewed = await h.ProgressAsync(path.ItemId, "ana");
        var records = await h.RecordsAsync("ana");

        // Assert
        Assert.Equal(CertificationPathState.Lapsed, lapsed.State);
        Assert.Equal(CertificationPathState.Certified, renewed.State);
        Assert.Equal(2, records.Count);
        Assert.Contains(records, record => record.PreviousRecordId == first.CurrentRecordId);
    }

    [Fact]
    public async Task Revocation_StopsTheProgress_AndARestartNeedsNewEvidence()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        var path = await h.AddPathAsync(join: RequirementJoin.Any);

        await h.CompleteCourseAsync("ana", "safety");

        var first = await h.ProgressAsync(path.ItemId, "ana");

        // Act
        await h.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().RevokeAsync(first.CurrentRecordId, "auditor", "The exam was voided"));

        var revoked = await h.ProgressAsync(path.ItemId, "ana");
        var evaluate = await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().EvaluateAsync(path.ItemId, "ana"));

        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(1);
        var restart = await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().StartAsync(path.ItemId, "ana", "admin"));
        var records = await h.RecordsAsync("ana");

        // Assert
        Assert.Equal(CertificationPathState.Revoked, revoked.State);
        Assert.Null(evaluate.IssuedRecordId);
        Assert.Equal(CertificationPathState.InProgress, restart.Progress.State);
        Assert.Single(records);
    }

    [Fact]
    public async Task Save_RefusesAPathWithoutRequirements_AndASecondPathForTheSameCertification()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        var path = await h.AddPathAsync();

        // Act
        var empty = await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().SaveAsync(new CertificationPath { CertificationId = path.CertificationId }));
        var second = await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().SaveAsync(new CertificationPath
        {
            CertificationId = path.CertificationId,
            InitialRequirements = [new CertificationRequirement { Kind = CertificationsConstants.RequirementKinds.CourseCompleted, TargetId = "safety" }],
        }));

        // Assert
        Assert.False(empty.Succeeded);
        Assert.False(second.Succeeded);
    }

    [Theory]
    [InlineData(CertificationReferenceDate.CompletionDate, "2026-11-10", null, "2027-11-09")]
    [InlineData(CertificationReferenceDate.ExpiryDate, "2027-09-20", "2027-11-09", "2028-11-09")]
    [InlineData(CertificationReferenceDate.ExpiryDate, "2029-01-05", "2027-11-09", "2029-11-09")]
    [InlineData(CertificationReferenceDate.CompletionDate, "2027-09-20", "2027-11-09", "2028-09-19")]
    public void Expiry_FollowsTheReferenceDate(CertificationReferenceDate reference, string issuedOn, string previous, string expected)
    {
        // Arrange
        var path = new CertificationPath { ReferenceDate = reference };

        // Act
        var expiry = CertificationDates.Expiry(path, 12, DateOnly.Parse(issuedOn), previous is null ? null : DateOnly.Parse(previous));

        // Assert
        Assert.Equal(DateOnly.Parse(expected), expiry);
    }

    [Theory]
    [InlineData("2026-11-10", 0, "2026-12-31")]
    [InlineData("2026-11-10", 3, "2027-12-31")]
    public void FixedDate_ExpiresOnTheSameDay_AfterTheMinimumInterval(string issuedOn, int minimumMonths, string expected)
    {
        // Arrange
        var path = new CertificationPath { ReferenceDate = CertificationReferenceDate.FixedDate, FixedMonth = 12, FixedDay = 31, MinimumIntervalMonths = minimumMonths };

        // Act
        var expiry = CertificationDates.Expiry(path, 12, DateOnly.Parse(issuedOn), previousExpiresOn: null);

        // Assert
        Assert.Equal(DateOnly.Parse(expected), expiry);
    }

    [Fact]
    public void NeverExpiringCertification_HasNoExpiry()
    {
        Assert.Null(CertificationDates.Expiry(new CertificationPath(), null, new DateOnly(2026, 11, 10), null));
    }
}

/// <summary>
/// Learning, Assessments, Skills and certification paths on one database, with a course, an exam and a certification.
/// </summary>
internal sealed class CertificationHarness : IAsyncDisposable
{
    private CertificationHarness(AssessmentTestHarness inner)
    {
        Inner = inner;
    }

    public static DateOnly Today => DateOnly.FromDateTime(LearningTestHarness.Now);

    public AssessmentTestHarness Inner { get; }

    public TestClock Clock => Inner.Clock;

    public static async Task<CertificationHarness> CreateAsync(bool dropCompletionEvents = false, Action<IServiceCollection> configure = null)
    {
        AssessmentTestHarness inner = null;

        inner = await AssessmentTestHarness.CreateAsync(
            services =>
            {
                services.AddSkillsCore();
                services.AddLearningCertificationsCore();
                services.AddLearningCertificationsAssessments();
                services.RemoveAll<CertificateHolderResolver>();
                services.AddScoped<CertificateHolderResolver, NamedHolderResolver>();
                services.TryAddSingleton(Mock.Of<IContentManager>());
                services.AddSingleton<ILocalClock>(provider => new UtcLocalClock(provider.GetRequiredService<IClock>()));

                configure?.Invoke(services);

                if (dropCompletionEvents)
                {
                    // Every completion event is lost, as when an observer fails after the commit.
                    services.RemoveAll<IEnrollmentObserver>();
                    services.RemoveAll<CrestApps.OrchardCore.Learning.Assessments.Services.IAttemptObserver>();
                }
            },
            async schemaBuilder =>
            {
                await SkillsIndexSchema.CreateAsync(schemaBuilder);
                await CertificationsIndexSchema.CreateAsync(schemaBuilder);
            },
            [.. SkillsIndexProviders.All(), .. CertificationsIndexProviders.All(), new LeasedWorkItemIndexProvider<CertificationRenewalWork>(CertificationsConstants.RenewalWorkCollectionName)],
            [SkillsConstants.CollectionName, CertificationsConstants.CollectionName, CertificationsConstants.RenewalWorkCollectionName]);

        inner.Courses.AddCourse("safety");
        inner.Courses.AddItem("safety", "lesson");
        inner.Courses.AddCourse("exams");
        inner.Questions.AddChoice("q1", "a", options: ["a", "b"]);
        inner.AddQuiz("exams", "final", quiz =>
        {
            quiz.QuestionIds = ["q1"];
            quiz.MaxAttempts = null;
        });

        return new CertificationHarness(inner);
    }

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work) => Inner.RunAsync(work);

    public Task RunAsync(Func<IServiceProvider, Task> work) => Inner.RunAsync<object>(async services =>
    {
        await work(services);

        return null;
    });

    public async Task<CertificationPath> AddPathAsync(RequirementJoin join = RequirementJoin.All, Action<CertificationPath> configure = null)
    {
        var certification = await RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ICertificationManager>();
            var created = await manager.NewAsync();

            created.Name = "Workplace Safety";
            created.ValidityMonths = 12;
            created.GenerateNumbers = true;
            await manager.CreateAsync(created);

            return created;
        });

        var path = new CertificationPath
        {
            CertificationId = certification.ItemId,
            Join = join,
            InitialRequirements =
            [
                new CertificationRequirement { Kind = CertificationsConstants.RequirementKinds.CourseCompleted, TargetId = "safety" },
                new CertificationRequirement { Kind = CertificationsConstants.RequirementKinds.AssessmentPassed, TargetId = "final" },
            ],
        };

        configure?.Invoke(path);

        var result = await RunAsync(services => services.GetRequiredService<ICertificationPathService>().SaveAsync(path));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return path;
    }

    public async Task CompleteCourseAsync(string userId, string courseId)
    {
        var enrollment = await Inner.EnrollAsync(userId, courseId);

        await CompleteEnrollmentAsync(enrollment, courseId);
    }

    public async Task ReEnrollAndCompleteAsync(string userId, string courseId, string previousEnrollmentId)
    {
        var result = await RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "again-" + userId,
            PreviousEnrollmentId = previousEnrollmentId,
        }));

        Assert.True(result.Succeeded, result.ErrorCode);
        await CompleteEnrollmentAsync(result.Enrollment, courseId);
    }

    public async Task CompleteEnrollmentAsync(Enrollment enrollment, string courseId)
    {
        await RunAsync(async services =>
        {
            var fresh = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId);

            return await services.GetRequiredService<ILearnerProgressService>().RecordViewedAsync(fresh, "lesson", 100);
        });

        var completed = (await EnrollmentsAsync(enrollment.UserId, courseId)).Single(item => item.ItemId == enrollment.ItemId);

        Assert.Equal(EnrollmentStatus.Completed, completed.Status);
    }

    public async Task PassQuizAsync(string userId)
    {
        await Inner.EnrollAsync(userId, "exams");

        var started = await Inner.StartAsync(userId, "final");

        Assert.True(started.Succeeded, started.ErrorCode);
        await Inner.RunAsync(service => service.SaveResponseAsync(started.Attempt.ItemId, userId, 0, new AttemptResponse { ChoiceIds = ["a"] }));

        var submitted = await Inner.RunAsync(service => service.SubmitAsync(started.Attempt.ItemId, userId));

        Assert.True(submitted.Attempt.Passed, "The attempt should pass.");
    }

    public Task<IReadOnlyList<Enrollment>> EnrollmentsAsync(string userId, string courseId)
        => RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync(userId, courseId));

    public Task<CertificationPathProgress> ProgressAsync(string pathId, string userId)
        => RunAsync(services => services.GetRequiredService<ICertificationPathService>().FindProgressAsync(pathId, userId));

    public Task<IReadOnlyList<CertificationRecord>> RecordsAsync(string userId)
        => RunAsync(services => services.GetRequiredService<ICertificationRecordService>().GetForSubjectAsync(SkillSubject.User(userId)));

    public ValueTask DisposeAsync() => Inner.DisposeAsync();

    private sealed class NamedHolderResolver : CertificateHolderResolver
    {
        public NamedHolderResolver()
            : base(null, null)
        {
        }

        public override Task<(SkillSubject Subject, string Name)> ResolveAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult((SkillSubject.User(userId), "Name of " + userId));

        public override Task<IReadOnlyList<SkillSubject>> ListSubjectsAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SkillSubject>>([SkillSubject.User(userId)]);
    }

    private sealed class UtcLocalClock : ILocalClock
    {
        private readonly IClock _clock;

        public UtcLocalClock(IClock clock)
        {
            _clock = clock;
        }

        public Task<DateTimeOffset> GetLocalNowAsync() => Task.FromResult(new DateTimeOffset(_clock.UtcNow, TimeSpan.Zero));

        public Task<ITimeZone> GetLocalTimeZoneAsync() => Task.FromResult<ITimeZone>(null);

        public Task<DateTimeOffset> ConvertToLocalAsync(DateTimeOffset dateTimeOffset) => Task.FromResult(dateTimeOffset);

        public Task<DateTime> ConvertToUtcAsync(DateTime dateTime) => Task.FromResult(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
    }
}
