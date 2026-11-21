using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Tests.Learning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// Certificates the site issues itself: numbered, with a verification code, one per source however often the event
/// arrives, renewed as a new linked record, revoked with who and why, and heard by observers after the commit.
/// </summary>
public sealed class CertificateIssuerTests
{
    private static readonly SkillSubject _ana = SkillSubject.User("ana");

    [Fact]
    public async Task IssueAsync_NumbersTheCertificate_GivesItAVerificationCode_AndWorksOutTheExpiry()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var cpr = await harness.AddCertificationAsync("CPR", certification =>
        {
            certification.ValidityMonths = 24;
            certification.GenerateNumbers = true;
            certification.NumberPrefix = "CPR-";
        });

        // Act
        var first = await IssueAsync(harness, Request(cpr.ItemId, _ana, "path-1:ana:1"));
        var second = await IssueAsync(harness, Request(cpr.ItemId, SkillSubject.User("ben"), "path-1:ben:1"));

        // Assert
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.False(first.AlreadyIssued);
        Assert.Equal("CPR-000001", first.Record.Number);
        Assert.Equal("CPR-000002", second.Record.Number);
        Assert.Equal(CertificationStatus.Valid, first.Record.Status);
        Assert.Equal(new DateOnly(2028, 10, 31), first.Record.ExpiresOn);
        Assert.Equal("Learning.CertificationPath", first.Record.Source);
        Assert.Equal("path-1:ana:1", first.Record.SourceId);
        Assert.Equal("Course and final exam passed", first.Record.Evidence);
        Assert.Equal(SkillsConstants.VerificationCodeLength, first.Record.VerificationCode.Length);
        Assert.All(first.Record.VerificationCode, character => Assert.Contains(character, ReadableCodes.Alphabet));
        Assert.NotEqual(first.Record.VerificationCode, second.Record.VerificationCode);
    }

    [Fact]
    public async Task IssueAsync_WithoutGeneratedNumbers_LeavesTheNumberEmpty()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var gas = await harness.AddCertificationAsync("Gas safety");

        // Act
        var result = await IssueAsync(harness, Request(gas.ItemId, _ana, "one"));

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Null(result.Record.Number);
        Assert.Null(result.Record.ExpiresOn);
    }

    [Fact]
    public async Task IssueAsync_TwiceForTheSameSource_ReturnsTheCertificateAlreadyIssued()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var cpr = await harness.AddCertificationAsync("CPR", certification => certification.GenerateNumbers = true);

        // Act
        var first = await IssueAsync(harness, Request(cpr.ItemId, _ana, "path-1:ana:1"));
        var again = await IssueAsync(harness, Request(cpr.ItemId, _ana, "path-1:ana:1"));
        var records = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().GetForSubjectAsync(_ana), commit: false);

        // Assert
        Assert.True(again.Succeeded);
        Assert.True(again.AlreadyIssued);
        Assert.Equal(first.Record.ItemId, again.Record.ItemId);
        Assert.Single(records);
    }

    [Fact]
    public async Task IssueAsync_CalledTenTimesAtOnce_IssuesOneCertificate()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync(services => services.Replace(ServiceDescriptor.Singleton<IDistributedLock, KeyedLock>()));
        var cpr = await harness.AddCertificationAsync("CPR", certification => certification.GenerateNumbers = true);

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => IssueAsync(harness, Request(cpr.ItemId, _ana, "path-1:ana:1")))));
        var records = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().GetForSubjectAsync(_ana), commit: false);

        // Assert
        Assert.All(results, result => Assert.True(result.Succeeded, string.Join(" ", result.Errors)));
        Assert.Single(results, result => !result.AlreadyIssued);
        Assert.Single(results.Select(result => result.Record.ItemId).Distinct());
        Assert.Single(records);
    }

    [Fact]
    public async Task IssueAsync_RefusesAMissingOrDisabledCertification_AMissingPerson_AndAMissingSource()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var retired = await harness.AddCertificationAsync("Retired", certification => certification.Enabled = false);
        var cpr = await harness.AddCertificationAsync("CPR");

        // Act
        var missing = await IssueAsync(harness, Request("missing", _ana, "one"));
        var disabled = await IssueAsync(harness, Request(retired.ItemId, _ana, "one"));
        var nobody = await IssueAsync(harness, Request(cpr.ItemId, default, "one"));
        var noSource = await IssueAsync(harness, Request(cpr.ItemId, _ana, null));

        // Assert
        Assert.False(missing.Succeeded);
        Assert.False(disabled.Succeeded);
        Assert.False(nobody.Succeeded);
        Assert.False(noSource.Succeeded);
    }

    [Fact]
    public async Task RenewAsync_IssuesANewLinkedCertificate_KeepsTheOldOne_AndRenewsOnce()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var cpr = await harness.AddCertificationAsync("CPR", certification =>
        {
            certification.ValidityMonths = 12;
            certification.GenerateNumbers = true;
        });
        var first = await IssueAsync(harness, Request(cpr.ItemId, _ana, "path-1:ana:1"));
        var renewal = Request(cpr.ItemId, _ana, "path-1:ana:2");

        renewal.IssuedOn = new DateOnly(2027, 10, 20);
        renewal.ExpiresOn = new DateOnly(2028, 11, 1);

        // Act
        var renewed = await harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().RenewAsync(first.Record.ItemId, renewal));
        var again = await harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().RenewAsync(first.Record.ItemId, renewal));
        var previous = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().FindByIdAsync(first.Record.ItemId), commit: false);
        var records = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().GetForSubjectAsync(_ana), commit: false);

        // Assert
        Assert.True(renewed.Succeeded, string.Join(" ", renewed.Errors));
        Assert.False(renewed.AlreadyIssued);
        Assert.True(again.AlreadyIssued);
        Assert.Equal(renewed.Record.ItemId, again.Record.ItemId);
        Assert.Equal(first.Record.ItemId, renewed.Record.PreviousRecordId);
        Assert.Equal(renewed.Record.ItemId, previous.RenewedByRecordId);
        Assert.Equal(new DateOnly(2027, 10, 31), previous.ExpiresOn);
        Assert.Equal(CertificationStatus.Valid, previous.Status);
        Assert.Equal(new DateOnly(2028, 11, 1), renewed.Record.ExpiresOn);
        Assert.NotEqual(first.Record.Number, renewed.Record.Number);
        Assert.NotEqual(first.Record.VerificationCode, renewed.Record.VerificationCode);
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task RevokeAsync_RecordsWhoAndWhyAndWhen_WithoutTouchingTheNotes()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var cpr = await harness.AddCertificationAsync("CPR");
        var issued = await IssueAsync(harness, Request(cpr.ItemId, _ana, "one"));

        // Act
        var revoked = await harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().RevokeAsync(issued.Record.ItemId, "auditor", "The final exam was voided"));
        var saved = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().FindByIdAsync(issued.Record.ItemId), commit: false);

        // Assert
        Assert.True(revoked.Succeeded, string.Join(" ", revoked.Errors));
        Assert.Equal(CertificationStatus.Revoked, saved.Status);
        Assert.Equal("The final exam was voided", saved.RevokedReason);
        Assert.Equal("auditor", saved.RevokedBy);
        Assert.Equal(SkillsTestHarness.Now, saved.RevokedUtc);
        Assert.Null(saved.Notes);
    }

    [Fact]
    public async Task IssueAsync_AfterARevocation_IssuesANewCertificateForTheSameSource()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var cpr = await harness.AddCertificationAsync("CPR");
        var issued = await IssueAsync(harness, Request(cpr.ItemId, _ana, "one"));

        await harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().RevokeAsync(issued.Record.ItemId, "auditor", "Voided"));

        // Act
        var again = await IssueAsync(harness, Request(cpr.ItemId, _ana, "one"));

        // Assert
        Assert.True(again.Succeeded);
        Assert.False(again.AlreadyIssued);
        Assert.NotEqual(issued.Record.ItemId, again.Record.ItemId);
    }

    [Fact]
    public async Task Observers_HearIssuedRenewedAndRevoked_OnceEach_AfterTheCommit()
    {
        // Arrange
        var observer = new RecordingObserver();
        await using var harness = await CreateHarnessAsync(services => services.AddSingleton<ICertificationRecordObserver>(observer));
        var cpr = await harness.AddCertificationAsync("CPR");

        observer.Harness = harness;

        // Act
        var issued = await IssueAsync(harness, Request(cpr.ItemId, _ana, "one"));

        await IssueAsync(harness, Request(cpr.ItemId, _ana, "one"));

        var renewed = await harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().RenewAsync(issued.Record.ItemId, Request(cpr.ItemId, _ana, "two")));

        await harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().RevokeAsync(renewed.Record.ItemId, "auditor", "Voided"));

        // Assert
        Assert.Equal(["issued", "renewed", "revoked"], observer.Events.Select(item => item.Event));
        Assert.All(observer.Events, item => Assert.True(item.Committed, $"The {item.Event} observer ran before the commit."));
        Assert.Equal(issued.Record.ItemId, observer.Events[1].Context.PreviousRecord.ItemId);
        Assert.Equal("CPR", observer.Events[0].Context.Certification.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FindByVerificationCodeAsync_IgnoresCaseSpacesAndDashes(bool typedLoosely)
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var cpr = await harness.AddCertificationAsync("CPR");
        var issued = await IssueAsync(harness, Request(cpr.ItemId, _ana, "one"));
        var code = issued.Record.VerificationCode;
        var typed = typedLoosely
            ? $" {code[..4].ToLowerInvariant()}-{code[4..8]} {code[8..12]}-{code[12..].ToLowerInvariant()} "
            : code;

        // Act
        var found = await harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().FindByVerificationCodeAsync(typed), commit: false);
        var wrong = await harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().FindByVerificationCodeAsync("AAAA-AAAA-AAAA-AAAA"), commit: false);

        // Assert
        Assert.Equal(issued.Record.ItemId, found?.ItemId);
        Assert.Null(wrong);
    }

    [Fact]
    public async Task DailyCheck_SendsNoReminderForARenewedCertificate()
    {
        // Arrange
        var observer = new ExpiryObserver();
        await using var harness = await CreateHarnessAsync(services => services.AddSingleton<ICertificationExpiryObserver>(observer));
        var cpr = await harness.AddCertificationAsync("CPR", certification =>
        {
            certification.ValidityMonths = 12;
            certification.ReminderDays = [30];
        });
        var first = Request(cpr.ItemId, _ana, "one");

        first.IssuedOn = SkillsTestHarness.Today.AddYears(-1).AddDays(10);

        var issued = await IssueAsync(harness, first);

        await harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().RenewAsync(issued.Record.ItemId, Request(cpr.ItemId, _ana, "two")));

        // Act (the first certificate ends in nine days, inside its 30-day reminder, and again after it has ended)
        await harness.RunAsync(services => services.GetRequiredService<SkillsDailyCheck>().RunAsync(SkillsTestHarness.Today));
        await harness.RunAsync(services => services.GetRequiredService<SkillsDailyCheck>().RunAsync(SkillsTestHarness.Today.AddDays(20)));

        var old = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().FindByIdAsync(issued.Record.ItemId), commit: false);

        // Assert
        Assert.Empty(observer.Expiring);
        Assert.Empty(observer.Expired);
        Assert.Equal(CertificationStatus.Expired, old.Status);
    }

    private static Task<SkillsTestHarness> CreateHarnessAsync(Action<IServiceCollection> configure = null)
    {
        return SkillsTestHarness.CreateAsync(services =>
        {
            services.AddNumberSequence();
            configure?.Invoke(services);
        });
    }

    private static CertificateIssueRequest Request(string certificationId, SkillSubject subject, string sourceId)
    {
        return new CertificateIssueRequest
        {
            CertificationId = certificationId,
            Subject = subject,
            SubjectName = "Ana Lima",
            Source = "Learning.CertificationPath",
            SourceId = sourceId,
            Evidence = "Course and final exam passed",
            IssuedOn = SkillsTestHarness.Today.AddDays(-1),
            Actor = "Learning",
        };
    }

    private static Task<CertificateIssueResult> IssueAsync(SkillsTestHarness harness, CertificateIssueRequest request)
    {
        return harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().IssueAsync(request));
    }

    private sealed class RecordingObserver : ICertificationRecordObserver
    {
        public SkillsTestHarness Harness { get; set; }

        public List<(string Event, CertificationRecordContext Context, bool Committed)> Events { get; } = [];

        public async Task IssuedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
            => Events.Add(("issued", context, await IsCommittedAsync(context.Record.ItemId, null)));

        public async Task RenewedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
            => Events.Add(("renewed", context, await IsCommittedAsync(context.Record.ItemId, null)));

        public async Task RevokedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
            => Events.Add(("revoked", context, await IsCommittedAsync(context.Record.ItemId, CertificationStatus.Revoked)));

        // Another scope reads the database, so it sees only what was committed.
        private async Task<bool> IsCommittedAsync(string id, CertificationStatus? status)
        {
            var stored = await Harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().FindByIdAsync(id), commit: false);

            return stored is not null && (status is null || stored.Status == status);
        }
    }

    private sealed class ExpiryObserver : ICertificationExpiryObserver
    {
        public List<string> Expiring { get; } = [];

        public List<string> Expired { get; } = [];

        public Task ExpiringAsync(CertificationRecord record, Certification certification, int daysBefore, CancellationToken cancellationToken = default)
        {
            Expiring.Add(record.ItemId);

            return Task.CompletedTask;
        }

        public Task ExpiredAsync(CertificationRecord record, Certification certification, CancellationToken cancellationToken = default)
        {
            Expired.Add(record.ItemId);

            return Task.CompletedTask;
        }
    }
}
