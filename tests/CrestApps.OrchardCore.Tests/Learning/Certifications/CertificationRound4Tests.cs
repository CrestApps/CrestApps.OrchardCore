using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Certifications;

/// <summary>
/// Review round 4, findings 3 and 4 and the last valid day: the upkeep pages through everyone in a stable order and
/// leaves disabled paths out of the due renewals; a second revocation changes nothing and tells nobody; a certificate
/// issued by a path and one recorded by hand end on the same day, and both count on it.
/// </summary>
public sealed class CertificationRound4Tests
{
    [Fact]
    public async Task WorkingPeople_ArePagedInAStableOrder_ToTheLastOne()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();

        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<CertificationPathProgressStore>();

            foreach (var user in new[] { "u1", "u2", "u3" })
            {
                await store.CreateAsync(new CertificationPathProgress { PathId = "p1", UserId = user, State = CertificationPathState.InProgress });
            }

            await store.CreateAsync(new CertificationPathProgress { PathId = "p1", UserId = "done", State = CertificationPathState.Certified });
        });

        // Act
        var (first, second) = await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<CertificationPathProgressStore>();
            var states = new[] { CertificationPathState.InProgress };
            var page1 = await store.PageUserIdsByStatesAsync("p1", states, 0, 2);
            var page2 = await store.PageUserIdsByStatesAsync("p1", states, page1[^1].RowId, 2);

            return (page1, page2);
        });

        // Assert
        Assert.Equal(["u1", "u2"], first.Select(row => row.UserId));
        Assert.Equal(["u3"], second.Select(row => row.UserId));
    }

    [Fact]
    public async Task DueRenewals_LeaveDisabledPathsOut()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        var today = CertificationHarness.Today;

        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<CertificationPathProgressStore>();

            await store.CreateAsync(new CertificationPathProgress { PathId = "disabled", UserId = "a", State = CertificationPathState.Certified, RenewalOpensOn = today.AddDays(-1) });
            await store.CreateAsync(new CertificationPathProgress { PathId = "enabled", UserId = "b", State = CertificationPathState.Certified, RenewalOpensOn = today.AddDays(-1) });
        });

        // Act
        var due = await h.RunAsync(services => services.GetRequiredService<CertificationPathProgressStore>()
            .PageRenewalDueAsync(["enabled"], CrestApps.OrchardCore.Learning.Certifications.Core.CertificationDates.ToKey(today).Value, 0, 10));

        // Assert
        Assert.Single(due);
    }

    [Fact]
    public async Task RevokingTwice_KeepsTheFirstRevocation_AndTellsNobodyAgain()
    {
        // Arrange
        var observer = new CountingObserver();
        await using var h = await CertificationHarness.CreateAsync(configure: services => services.AddSingleton<ICertificationRecordObserver>(observer));
        await h.AddPathAsync();
        await h.CompleteCourseAsync("ana", "safety");
        await h.PassQuizAsync("ana");
        var record = Assert.Single(await h.RecordsAsync("ana"));

        // Act
        var first = await h.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().RevokeAsync(record.ItemId, "first", "Fraud"));
        var second = await h.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().RevokeAsync(record.ItemId, "second", "Again"));
        var revoked = Assert.Single(await h.RecordsAsync("ana"));

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal("first", revoked.RevokedBy);
        Assert.Equal("Fraud", revoked.RevokedReason);
        Assert.Equal(1, observer.Revoked);
    }

    [Fact]
    public async Task APathCertificate_AndAHandRecordedOne_EndOnTheSameDay_AndCountOnIt()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        await h.AddPathAsync();
        await h.CompleteCourseAsync("ana", "safety");
        await h.PassQuizAsync("ana");
        var issued = Assert.Single(await h.RecordsAsync("ana"));

        // Act
        var byHand = await h.RunAsync(async services =>
        {
            var record = new CertificationRecord { SubjectKind = SkillSubject.User("ben").Kind, SubjectId = "ben", CertificationId = issued.CertificationId, IssuedOn = issued.IssuedOn };

            await services.GetRequiredService<ICertificationRecordService>().SaveAsync(record, "admin");

            return record;
        });

        // Assert
        Assert.Equal(issued.ExpiresOn, byHand.ExpiresOn);
        Assert.True(issued.IsValidOn(issued.ExpiresOn.Value));
        Assert.False(issued.IsValidOn(issued.ExpiresOn.Value.AddDays(1)));
    }

    private sealed class CountingObserver : ICertificationRecordObserver
    {
        public int Revoked { get; private set; }

        public Task RevokedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        {
            Revoked++;

            return Task.CompletedTask;
        }
    }
}
