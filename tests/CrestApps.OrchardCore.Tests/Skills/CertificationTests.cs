using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// Certification records: expiry worked out from the certification, checking and withdrawing, the "expiring soon"
/// list, and the daily check that raises each reminder and each expiry once.
/// </summary>
public sealed class CertificationTests
{
    private static readonly SkillSubject _ben = SkillSubject.Worker("ben");

    [Fact]
    public async Task SaveAsync_WorksOutTheExpiryFromTheValidity_WhenTheRecordHasNone()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var gas = await harness.AddCertificationAsync("Gas safety", certification => certification.ValidityMonths = 24);

        // Act
        var record = await SkillsTests.SaveRecordAsync(harness, _ben, gas.ItemId, new DateOnly(2026, 3, 15), CertificationStatus.Valid);
        var ownExpiry = await SkillsTests.SaveRecordAsync(harness, SkillSubject.Worker("cara"), gas.ItemId, new DateOnly(2026, 3, 15), CertificationStatus.Valid, new DateOnly(2026, 12, 31));

        // Assert (a certificate issued on 15 March 2026 for 24 months is valid through 14 March 2028)
        Assert.Equal(new DateOnly(2028, 3, 14), record.ExpiresOn);
        Assert.Equal(new DateOnly(2026, 12, 31), ownExpiry.ExpiresOn);
    }

    [Fact]
    public async Task SaveAsync_RefusesAnUnknownCertification_AMissingPerson_AndExpiryBeforeIssue()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var gas = await harness.AddCertificationAsync("Gas safety");

        // Act
        var unknown = await SaveAsync(harness, new CertificationRecord { SubjectKind = SkillSubjectKind.Worker, SubjectId = "ben", CertificationId = "missing" });
        var nobody = await SaveAsync(harness, new CertificationRecord { CertificationId = gas.ItemId });
        var backwards = await SaveAsync(harness, new CertificationRecord
        {
            SubjectKind = SkillSubjectKind.Worker,
            SubjectId = "ben",
            CertificationId = gas.ItemId,
            IssuedOn = new DateOnly(2026, 5, 1),
            ExpiresOn = new DateOnly(2026, 4, 1),
        });

        // Assert
        Assert.False(unknown.Succeeded);
        Assert.False(nobody.Succeeded);
        Assert.False(backwards.Succeeded);
    }

    [Fact]
    public async Task VerifyAsync_NeedsTheDocument_WhenTheCertificationAsksForOne()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var forklift = await harness.AddCertificationAsync("Forklift", certification => certification.RequiresDocument = true);
        var record = await SkillsTests.SaveRecordAsync(harness, _ben, forklift.ItemId, SkillsTestHarness.Today, CertificationStatus.Pending);

        // Act
        var withoutDocument = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().VerifyAsync(record.ItemId, "manager"));

        record.DocumentPath = "certificates/ben-forklift.pdf";
        await SaveAsync(harness, record);

        var withDocument = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().VerifyAsync(record.ItemId, "manager"));
        var saved = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().FindByIdAsync(record.ItemId), commit: false);

        // Assert
        Assert.False(withoutDocument.Succeeded);
        Assert.True(withDocument.Succeeded, string.Join(" ", withDocument.Errors));
        Assert.Equal(CertificationStatus.Valid, saved.Status);
        Assert.Equal("manager", saved.VerifiedBy);
        Assert.Equal(SkillsTestHarness.Now, saved.VerifiedUtc);
    }

    [Fact]
    public async Task IsValidOnAsync_CountsOnlyCheckedCertificatesInsideTheirDates_AndNotRevokedOnes()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var gas = await harness.AddCertificationAsync("Gas safety", certification => certification.ValidityMonths = 12);
        var pending = await SkillsTests.SaveRecordAsync(harness, SkillSubject.Worker("pending"), gas.ItemId, SkillsTestHarness.Today, CertificationStatus.Pending);
        var valid = await SkillsTests.SaveRecordAsync(harness, _ben, gas.ItemId, SkillsTestHarness.Today, CertificationStatus.Valid);

        // Act
        var pendingCounts = await IsValidAsync(harness, pending.Subject, gas.ItemId, SkillsTestHarness.Today);
        var validToday = await IsValidAsync(harness, _ben, gas.ItemId, SkillsTestHarness.Today);
        var validBeforeIssue = await IsValidAsync(harness, _ben, gas.ItemId, SkillsTestHarness.Today.AddDays(-1));
        var validAfterExpiry = await IsValidAsync(harness, _ben, gas.ItemId, SkillsTestHarness.Today.AddYears(1));
        var revoke = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().RevokeAsync(valid.ItemId, "manager", "Failed an audit"));
        var afterRevoke = await IsValidAsync(harness, _ben, gas.ItemId, SkillsTestHarness.Today);

        // Assert
        Assert.False(pendingCounts);
        Assert.True(validToday);
        Assert.False(validBeforeIssue);
        Assert.False(validAfterExpiry);
        Assert.True(revoke.Succeeded);
        Assert.False(afterRevoke);
    }

    [Fact]
    public async Task PageAsync_ListsTheCertificatesExpiringSoon_SoonestFirst()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var gas = await harness.AddCertificationAsync("Gas safety");

        await SkillsTests.SaveRecordAsync(harness, SkillSubject.Worker("later"), gas.ItemId, SkillsTestHarness.Today.AddYears(-1), CertificationStatus.Valid, SkillsTestHarness.Today.AddDays(20));
        await SkillsTests.SaveRecordAsync(harness, SkillSubject.Worker("sooner"), gas.ItemId, SkillsTestHarness.Today.AddYears(-1), CertificationStatus.Valid, SkillsTestHarness.Today.AddDays(5));
        await SkillsTests.SaveRecordAsync(harness, SkillSubject.Worker("far"), gas.ItemId, SkillsTestHarness.Today.AddYears(-1), CertificationStatus.Valid, SkillsTestHarness.Today.AddDays(200));

        // Act
        var (count, items) = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().PageAsync(new CertificationRecordQuery
        {
            Status = CertificationStatus.Valid,
            ExpiresOnOrBefore = SkillsTestHarness.Today.AddDays(30),
        }, 0, 10), commit: false);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(["sooner", "later"], items.Select(item => item.SubjectId));
    }

    [Fact]
    public async Task DailyCheck_RaisesEachReminderOnce_ThenTheExpiryOnce()
    {
        // Arrange
        var observer = new RecordingExpiryObserver();
        await using var harness = await SkillsTestHarness.CreateAsync(services => services.AddSingleton<ICertificationExpiryObserver>(observer));
        var gas = await harness.AddCertificationAsync("Gas safety", certification => certification.ReminderDays = [30, 7]);
        var record = await SkillsTests.SaveRecordAsync(harness, _ben, gas.ItemId, SkillsTestHarness.Today.AddYears(-1), CertificationStatus.Valid, SkillsTestHarness.Today.AddDays(25));

        // Act
        await RunDailyAsync(harness, SkillsTestHarness.Today);
        await RunDailyAsync(harness, SkillsTestHarness.Today);
        await RunDailyAsync(harness, SkillsTestHarness.Today.AddDays(18));
        await RunDailyAsync(harness, SkillsTestHarness.Today.AddDays(26));
        await RunDailyAsync(harness, SkillsTestHarness.Today.AddDays(27));
        var saved = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().FindByIdAsync(record.ItemId), commit: false);

        // Assert (25 days left on the first run is inside the 30-day reminder; the 7-day one comes on day 18)
        Assert.Equal(["expiring:30", "expiring:7", "expired"], observer.Events);
        Assert.Equal(CertificationStatus.Expired, saved.Status);
    }

    [Fact]
    public async Task DailyCheck_RemindsAgain_AfterTheCertificateIsRenewed()
    {
        // Arrange
        var observer = new RecordingExpiryObserver();
        await using var harness = await SkillsTestHarness.CreateAsync(services => services.AddSingleton<ICertificationExpiryObserver>(observer));
        var gas = await harness.AddCertificationAsync("Gas safety", certification => certification.ReminderDays = [30]);
        var record = await SkillsTests.SaveRecordAsync(harness, _ben, gas.ItemId, SkillsTestHarness.Today.AddYears(-1), CertificationStatus.Valid, SkillsTestHarness.Today.AddDays(10));

        await RunDailyAsync(harness, SkillsTestHarness.Today);

        // Act
        record = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().FindByIdAsync(record.ItemId), commit: false);
        record.ExpiresOn = SkillsTestHarness.Today.AddDays(380);
        await SaveAsync(harness, record);
        await RunDailyAsync(harness, SkillsTestHarness.Today.AddDays(355));

        // Assert
        Assert.Equal(["expiring:30", "expiring:30"], observer.Events);
    }

    [Fact]
    public async Task DailyCheck_TellsTheHandlers_AboutAssignmentsStartingOrEndingThatDay()
    {
        // Arrange
        var handler = new SkillsTests.RecordingAssignmentHandler();
        await using var harness = await SkillsTestHarness.CreateAsync(services => services.AddSingleton<ISkillAssignmentHandler>(handler));
        var spanish = await harness.AddSkillAsync("Spanish");
        var french = await harness.AddSkillAsync("French");

        await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().SetAsync(SkillSubject.User("starts"),
            [new SkillAssignmentInput { SkillId = spanish.ItemId, EffectiveFrom = SkillsTestHarness.Today.AddDays(3) }], "tester"));
        await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().SetAsync(SkillSubject.User("ends"),
            [new SkillAssignmentInput { SkillId = french.ItemId, EffectiveTo = SkillsTestHarness.Today.AddDays(2) }], "tester"));
        await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().SetAsync(SkillSubject.User("steady"),
            [new SkillAssignmentInput { SkillId = french.ItemId }], "tester"));
        handler.Contexts.Clear();

        // Act (day 3: one assignment starts today and the other ended yesterday)
        await RunDailyAsync(harness, SkillsTestHarness.Today.AddDays(3));

        // Assert
        Assert.Equal(["ends", "starts"], handler.Contexts.Select(context => context.Subject.Id).Order(StringComparer.Ordinal));
        Assert.All(handler.Contexts, context => Assert.True(context.EffectiveDatesOnly));
    }

    private static Task<SkillsResult> SaveAsync(SkillsTestHarness harness, CertificationRecord record)
    {
        return harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().SaveAsync(record, "tester"));
    }

    private static Task<bool> IsValidAsync(SkillsTestHarness harness, SkillSubject subject, string certificationId, DateOnly date)
    {
        return harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().IsValidOnAsync(subject, certificationId, date), commit: false);
    }

    private static Task RunDailyAsync(SkillsTestHarness harness, DateOnly today)
    {
        return harness.RunAsync(services => services.GetRequiredService<SkillsDailyCheck>().RunAsync(today));
    }

    private sealed class RecordingExpiryObserver : ICertificationExpiryObserver
    {
        public List<string> Events { get; } = [];

        public Task ExpiringAsync(CertificationRecord record, Certification certification, int daysBefore, CancellationToken cancellationToken = default)
        {
            Events.Add($"expiring:{daysBefore}");

            return Task.CompletedTask;
        }

        public Task ExpiredAsync(CertificationRecord record, Certification certification, CancellationToken cancellationToken = default)
        {
            Events.Add("expired");

            return Task.CompletedTask;
        }
    }
}
