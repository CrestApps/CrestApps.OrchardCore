using System.Diagnostics;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Migrations;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Guards the half-minute wait between an agent hanging up a bridged call and the agent's wrap-up. Both legs of the
/// call hung up at once, and both webhooks were processed side by side. The caller's leg took the call's ingestion
/// lease and needed to write the ended call. The agent's leg had already written its call-quality record, so its
/// open transaction held SQLite's only write lock, and it then waited for the same call's lease to end the call from
/// its side. Each waited on the other until the agent leg's lease wait gave up thirty seconds later; only then did
/// the call end, the agent's wrap-up start and the header stop saying "On a call".
/// </summary>
public sealed class VoiceIngressGateWriteLockTests
{
    private const string ProviderName = "Telnyx";
    private const string CallerCallId = "v3:caller-call";

    private static readonly DateTime _startedUtc = new(2026, 9, 24, 19, 42, 28, DateTimeKind.Utc);
    private static readonly DateTime _hungUpUtc = new(2026, 9, 24, 19, 43, 52, DateTimeKind.Utc);

    [Fact]
    public async Task AcquireAsync_WhenBothLegsOfACallHangUpAtOnce_TheCallEndsAndWrapUpStartsWithoutWaitingOutEitherTimeout()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"crestapps-ingress-write-lock-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedCallAsync(store);

            var distributedLock = new FakeDistributedLock();
            var callerLegHoldsTheLease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var agentLegHasWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopwatch = Stopwatch.StartNew();

            // The caller's leg hangs up: it serializes on the call, then ends it in the telephony call history (an
            // isolated write, the one that was blocked live) and in the Contact Center, which starts the wrap-up.
            var callerLeg = Task.Run(async () =>
            {
                await using var session = store.CreateSession();
                var gate = new VoiceIngressGate(distributedLock, session);

                await using (await gate.AcquireAsync(ProviderName, CallerCallId, TestContext.Current.CancellationToken))
                {
                    callerLegHoldsTheLease.SetResult();
                    await agentLegHasWritten.Task;

                    var interactionStore = new DefaultTelephonyInteractionStore(session, store, new ProviderIdentityResolver([]), []);

                    await interactionStore.UpdateByProviderCallIdAsync(
                        ProviderName,
                        CallerCallId,
                        call =>
                        {
                            call.Outcome = CallOutcome.Completed;
                            call.EndedUtc = _hungUpUtc;

                            return true;
                        },
                        TestContext.Current.CancellationToken);

                    await session.SaveAsync(new InteractionEvent
                    {
                        ItemId = "wrap-up",
                        InteractionId = "interaction-1",
                        EventType = ContactCenterConstants.Events.AgentStateChanged,
                        OccurredUtc = _hungUpUtc,
                        RecordedUtc = _hungUpUtc,
                    }, cancellationToken: TestContext.Current.CancellationToken);

                    await session.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
            }, TestContext.Current.CancellationToken);

            // The agent's leg hangs up at the same moment: it records the leg's call quality first, which a query
            // flushes into an open transaction, and then ends the call from its side through the same call's lease.
            var agentLeg = Task.Run(async () =>
            {
                await callerLegHoldsTheLease.Task;

                await using var session = store.CreateSession();
                var gate = new VoiceIngressGate(distributedLock, session);

                await session.SaveAsync(new CallQualityRecord
                {
                    ItemId = "agent-leg-quality",
                    RecordKey = "provider:agent-leg",
                    ProviderCallControlId = "v3:agent-leg",
                    ObservedUtc = _hungUpUtc,
                }, cancellationToken: TestContext.Current.CancellationToken);
                await session.FlushAsync(TestContext.Current.CancellationToken);

                agentLegHasWritten.SetResult();

                await using (await gate.AcquireAsync(ProviderName, CallerCallId, TestContext.Current.CancellationToken))
                {
                    // The call has already ended by the time the agent's leg gets its turn; nothing more to do.
                }

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }, TestContext.Current.CancellationToken);

            // Act
            await Task.WhenAll(callerLeg, agentLeg).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            stopwatch.Stop();

            // Assert
            Assert.Equal(CallOutcome.Completed, (await ReadCallAsync(store)).Outcome);
            Assert.NotNull(await ReadAsync<InteractionEvent>(store));

            // The agent leg's own work is kept, not lost to the lock it gave up.
            Assert.NotNull(await ReadAsync<CallQualityRecord>(store));

            // Promptly: neither the database's busy timeout nor the lease's timeout was waited out.
            Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(3));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task AcquireAsync_WhenTheLeaseIsFree_LeavesTheCallersOpenWorkUncommitted()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"crestapps-ingress-free-lease-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var gate = new VoiceIngressGate(new FakeDistributedLock(), session);

            await session.SaveAsync(new CallQualityRecord
            {
                ItemId = "uncontended-quality",
                RecordKey = "provider:uncontended",
            }, cancellationToken: TestContext.Current.CancellationToken);
            await session.FlushAsync(TestContext.Current.CancellationToken);

            // Act
            await using (await gate.AcquireAsync(ProviderName, CallerCallId, TestContext.Current.CancellationToken))
            {
                // Assert: nothing contended for the lease, so the caller's unit of work is still its own to finish.
                Assert.NotNull(session.CurrentTransaction);
            }

            await session.CancelAsync();

            Assert.Null(await ReadAsync<CallQualityRecord>(store));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        // A one-second busy timeout stands in for the live thirty seconds, so a write that cannot get the lock fails
        // quickly instead of stalling the test.
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False;Default Timeout=1"));
        store.RegisterIndexes([new TelephonyInteractionIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new TelephonyInteractionMigrations
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };
        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SeedCallAsync(IStore store)
    {
        await using var session = store.CreateSession();
        await session.SaveAsync(new TelephonyInteraction
        {
            InteractionId = "telephony-1",
            CallId = CallerCallId,
            ProviderName = ProviderName,
            UserId = "user-1",
            UserName = "agent",
            Direction = CallDirection.Inbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _startedUtc,
        }, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<TelephonyInteraction> ReadCallAsync(IStore store)
    {
        await using var session = store.CreateSession();

        return await session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.CallId == CallerCallId)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<T> ReadAsync<T>(IStore store)
        where T : class
    {
        await using var session = store.CreateSession();

        return await session.Query<T>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }
}
