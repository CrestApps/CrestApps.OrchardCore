using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using YesSql;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Review round 6, item 2: the time-off ledger copy runs from the tenant's start in a real shell. A tenant with an
/// entry in the old collection and no copy state, started again, ends with the entry in the time-off ledger and the
/// copy recorded complete.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class TimeOffLedgerCopyStartTests
{
    private readonly LearningSiteFixture _fixture;

    public TimeOffLedgerCopyStartTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ATenantStart_CopiesOldEntries_AndRecordsTheCopyComplete()
    {
        // Arrange: an entry as an install before the move kept it, and no record of a copy.
        var id = "old-entry-" + Guid.NewGuid().ToString("n")[..8];

        await _fixture.InTenantAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();

            foreach (var state in await session.Query<TimeOffLedgerCopyState>(collection: WorkforceConstants.CollectionName).ListAsync())
            {
                session.Delete(state, WorkforceConstants.CollectionName);
            }

            await session.SaveAsync(new TimeOffLedgerEntry
            {
                ItemId = id,
                WorkerId = "worker-1",
                PolicyId = "policy-1",
                Kind = TimeOffLedgerKind.Opening,
                Amount = 16,
                EffectiveDate = new DateOnly(2026, 1, 1),
                IdempotencyKey = "opening:" + id,
                CreatedUtc = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            }, checkConcurrency: false, collection: WorkforceConstants.CollectionName);
        });

        // Act: the tenant starts again, as after a deployment.
        var settings = _fixture.ShellHost.TryGetSettings(ShellSettings.DefaultShellName, out var found) ? found : throw new InvalidOperationException("No Default tenant.");

        await _fixture.ShellHost.ReleaseShellContextAsync(settings);

        var (copied, complete) = (false, false);

        for (var attempt = 0; attempt < 40 && !(copied && complete); attempt++)
        {
            (copied, complete) = await _fixture.InTenantAsync(async services =>
            {
                var rows = await services.GetRequiredService<ISession>()
                    .QueryIndex<QuantityLedgerEntryIndex>(index => index.ItemId == id, collection: WorkforceConstants.TimeOffLedgerCollectionName)
                    .ListAsync();

                return (rows.Count > 0, await services.GetRequiredService<TimeOffLedgerCopy>().IsCompleteAsync());
            });

            if (!(copied && complete))
            {
                await Task.Delay(250, TestContext.Current.CancellationToken);
            }
        }

        // Assert
        Assert.True(copied, "The entry was not copied to the time-off ledger.");
        Assert.True(complete, "The copy was not recorded complete.");
    }
}
