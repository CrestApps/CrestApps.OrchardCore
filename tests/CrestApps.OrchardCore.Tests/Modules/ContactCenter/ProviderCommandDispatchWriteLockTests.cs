using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Guards the voicemail command that was never marked done. Sending a ringing caller to voicemail flags the
/// interaction and records <c>CallSentToVoicemail</c> in the dispatching scope before the provider is called, and a
/// query flushes those writes into an open transaction. The outcome is then settled in a fresh scope, on a second
/// connection, which has to write too. On SQLite that is a second writer waiting on the first, and the first was
/// waiting on it: the settlement sat out the thirty-second busy timeout, failed with "database is locked", and took
/// the voicemail flag and event down with it. The command stayed "sent" until recovery trusted it seven minutes later
/// and re-projected a success, publishing a second <c>CallEnded</c> for a call that had long since ended.
/// </summary>
public sealed class ProviderCommandDispatchWriteLockTests
{
    private static readonly DateTime _now = new(2026, 9, 24, 21, 13, 34, DateTimeKind.Utc);

    [Fact]
    public async Task DispatchAsync_WhenTheExecutorWroteBeforeCallingTheProvider_SettlesInAFreshScopeWithoutWaitingOutTheWriteLock()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"crestapps-dispatch-write-lock-{Guid.NewGuid():N}.db");

        // A one-second busy timeout stands in for the live thirty, so a settlement blocked by the dispatcher's own
        // open transaction fails quickly instead of stalling the test.
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False;Default Timeout=1"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        try
        {
            await using var dispatchSession = store.CreateSession();
            var processor = CreateProcessor(store, dispatchSession);

            // Act
            var settled = await processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Confirmed, settled.Status);

            // What the dispatch wrote before calling the provider survived, and so did the settlement's own write.
            await using var readSession = store.CreateSession();
            var events = await readSession.Query<InteractionEvent>().ListAsync(TestContext.Current.CancellationToken);
            Assert.Contains(events, value => value.ItemId == "sent-to-voicemail");
            Assert.Contains(events, value => value.ItemId == "settled");
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static ProviderCommandProcessor CreateProcessor(IStore store, ISession dispatchSession)
    {
        var command = new ProviderCommand
        {
            CommandId = "command-1",
            CommandType = ProviderCommandType.SendToVoicemail,
            ProviderName = "Telnyx",
            InteractionId = "interaction-1",
            ActivityItemId = "activity-1",
        };
        command.Status = ProviderCommandStatus.Pending;
        var claim = new ProviderCommandClaim
        {
            CommandId = "command-1",
            FenceToken = 1,
            OwnerToken = "owner-1",
            LeaseExpiresUtc = _now.AddMinutes(5),
        };

        var manager = new Mock<IProviderCommandManager>();
        manager
            .Setup(value => value.FindByCommandIdAsync("command-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(command);

        var stateService = new Mock<IProviderCommandStateService>();
        stateService
            .Setup(value => value.TryClaimAsync("command-1", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);
        stateService
            .Setup(value => value.MarkSentAsync("command-1", claim, null, It.IsAny<CancellationToken>()))
            .Callback(() => command.Status = ProviderCommandStatus.Sent)
            .ReturnsAsync(command);
        stateService
            .Setup(value => value.StageConfirmSentAsync("command-1", claim, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => command.Status = ProviderCommandStatus.Confirmed)
            .ReturnsAsync(command);

        // The executor does what sending to voicemail does before the provider is called: it writes the voicemail
        // flag and event through the dispatching scope, and the publisher's duplicate check flushes them.
        var executor = new Mock<IProviderCommandTypeExecutor>();
        executor.SetupGet(value => value.CommandType).Returns(ProviderCommandType.SendToVoicemail);
        executor
            .Setup(value => value.CanDispatchAsync(It.IsAny<ProviderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        executor
            .Setup(value => value.ExecuteAsync(It.IsAny<ProviderCommand>(), It.IsAny<ProviderCommandClaim>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await dispatchSession.SaveAsync(NewEvent("sent-to-voicemail"), cancellationToken: TestContext.Current.CancellationToken);
                await dispatchSession.FlushAsync(TestContext.Current.CancellationToken);

                return new ContactCenterVoiceProviderResult
                {
                    Succeeded = true,
                    ProviderCallId = "v3:caller-call",
                    ProviderName = "Telnyx",
                };
            });

        ProviderCommandProcessor processor = null;

        // The settlement runs in a fresh scope with its own session and connection, and it writes.
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(value => value.ExecuteAsync<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()))
            .Returns(async (Func<IProviderCommandProcessor, Task> operation) =>
            {
                await using var settlementSession = store.CreateSession();
                await settlementSession.SaveAsync(NewEvent("settled"), cancellationToken: TestContext.Current.CancellationToken);
                await settlementSession.SaveChangesAsync(TestContext.Current.CancellationToken);

                await operation(processor);
            });

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        processor = new ProviderCommandProcessor(
            manager.Object,
            stateService.Object,
            new Mock<IActivityReservationService>().Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            [executor.Object],
            new DefaultTelephonyCommandExecutor(Options.Create(new TelephonyCommandOptions()), Mock.Of<IHostApplicationLifetime>()),
            scopeExecutor.Object,
            new TestContactCenterFeatureWorkManager(),
            dispatchSession,
            clock.Object,
            NullLogger<ProviderCommandProcessor>.Instance);

        return processor;
    }

    private static InteractionEvent NewEvent(string itemId)
        => new()
        {
            ItemId = itemId,
            InteractionId = "interaction-1",
            EventType = ContactCenterConstants.Events.CallSentToVoicemail,
            OccurredUtc = _now,
            RecordedUtc = _now,
        };
}
