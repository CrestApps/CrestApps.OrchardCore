using CrestApps.OrchardCore.Omnichannel.Automation;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Automation;

/// <summary>
/// A real SQLite database of automated conversations for the background passes that read the conversations awaiting a
/// customer's answer a batch at a time, so the index query, its document-id key and its filters are the ones that run.
/// </summary>
internal static class AutomatedActivityScanHarness
{
    public const string Channel = "TestChannel";
    public const string ReceivedEventType = "TestChannelReceived";
    public const string EndpointId = "endpoint-1";
    public const string EndpointAddress = "agent@example.com";
    public const string CadenceId = "cadence-1";
    public const string ProfileId = "profile-1";

    public static readonly DateTime Now = new(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Runs a test against a fresh database holding the activity index, and deletes it afterwards.
    /// </summary>
    public static async Task WithStoreAsync(string purpose, Func<IStore, Task> test)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"automated-scan-{purpose}-{Guid.NewGuid():N}.db");
        IStore store = null;

        try
        {
            store = await CreateStoreAsync($"Data Source={databasePath};Pooling=False");

            await test(store);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    /// <summary>
    /// Creates the automated channel the seeded conversations run on.
    /// </summary>
    public static IAutomatedMessagingChannel CreateChannel()
    {
        var channel = new Mock<IAutomatedMessagingChannel>();

        channel.SetupGet(x => x.Channel).Returns(Channel);
        channel.SetupGet(x => x.ReceivedEventType).Returns(ReceivedEventType);
        channel.SetupGet(x => x.ConversationNoun).Returns("test conversation");
        channel.Setup(x => x.NormalizeAddress(It.IsAny<string>())).Returns((string address) => address);

        return channel.Object;
    }

    /// <summary>
    /// Gets the AI chat session of the candidate with a number.
    /// </summary>
    public static string SessionId(int number)
        => $"session-{number:D3}";

    /// <summary>
    /// Gets the AI chat sessions of the candidates numbered from <paramref name="first"/> to <paramref name="last"/>.
    /// </summary>
    public static IEnumerable<string> SessionIds(int first, int last)
        => Enumerable.Range(first, last - first + 1).Select(SessionId);

    /// <summary>
    /// Seeds the candidates numbered 1 to <paramref name="count"/>, in that order, so their document ids rise with their
    /// numbers. After every tenth it seeds a conversation the passes must skip (a manual one, a finished one, one on a
    /// channel nobody automates), so the candidates' document ids are not consecutive and a filter that let any of them
    /// through would change what each batch holds.
    /// </summary>
    /// <returns>The candidates, by number: element 0 is candidate 1.</returns>
    public static async Task<IReadOnlyList<OmnichannelActivity>> SeedCandidatesAsync(IStore store, int count)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var candidates = new List<OmnichannelActivity>(count);

        await using var session = store.CreateSession();

        for (var number = 1; number <= count; number++)
        {
            var candidate = Activity(number, SessionId(number));

            await session.SaveAsync(candidate, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
            candidates.Add(candidate);

            if (number % 10 == 0)
            {
                await session.SaveAsync(NotACandidate(number), collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
            }
        }

        await session.SaveChangesAsync(cancellationToken);

        return candidates;
    }

    private static OmnichannelActivity NotACandidate(int number)
    {
        var activity = Activity(number, $"skipped-{number:D3}");

        activity.ItemId = $"skipped-{number:D3}";

        switch (number / 10 % 3)
        {
            case 0:
                activity.InteractionType = ActivityInteractionType.Manual;
                break;
            case 1:
                activity.Status = ActivityStatus.Completed;
                break;
            default:
                activity.Channel = "OtherChannel";
                break;
        }

        return activity;
    }

    private static OmnichannelActivity Activity(int number, string sessionId)
        => new()
        {
            ItemId = $"activity-{number:D3}",
            Channel = Channel,
            ChannelEndpointId = EndpointId,
            ContactContentItemId = $"contact-{number:D3}",
            ContactContentType = "Lead",
            PreferredDestination = $"customer-{number:D3}@example.com",
            ScheduledUtc = Now.AddDays(-1),
            CreatedUtc = Now.AddDays(-1),
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.AwaitingCustomerAnswer,
            AISessionId = sessionId,
            AIProfileId = ProfileId,
            CadenceId = CadenceId,
        };

    private static async Task<IStore> CreateStoreAsync(string connectionString)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = StoreFactory.Create(configuration => configuration.UseSqLite(connectionString));

        store.RegisterIndexes([new OmnichannelActivityIndexProvider()]);
        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, cancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<ActivityKind>("Kind")
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("DialerProfileId", column => column.Nullable().WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
            .Column<ActivityAssignmentStatus>("AssignmentStatus")
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<ActivityUrgencyLevel>("UrgencyLevel")
            .Column<ActivityStatus>("Status")
            .Column<ActivityInteractionType>("InteractionType")
            .Column<bool>("AiEscalated")
            .Column<string>("TerminalReasonCode", column => column.Nullable().WithLength(64)),
            collection: OmnichannelConstants.CollectionName);

        await transaction.CommitAsync(cancellationToken);

        return store;
    }
}

/// <summary>
/// A conversation gate that records every conversation a pass asks about. Both passes ask it first about each
/// conversation they examine, so the record is the order the pass read them in. While it reports every conversation as
/// generating, the pass examines and skips each one without touching anything else.
/// </summary>
internal sealed class RecordingConversationGate : IAutomatedConversationGate
{
    /// <summary>
    /// Gets the AI chat sessions asked about, in order.
    /// </summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether every conversation is reported as having a reply being composed.
    /// </summary>
    public bool ReportsGenerating { get; set; } = true;

    /// <summary>
    /// Gets or sets the AI chat session whose examination fails, as a failed read would.
    /// </summary>
    public string FailFor { get; set; }

    public IAutomatedConversationGeneration Begin(string sessionId, CancellationToken hostToken)
        => throw new NotSupportedException();

    public bool IsGenerating(string sessionId)
    {
        Asked.Add(sessionId);

        if (string.Equals(sessionId, FailFor, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The examination of this conversation failed.");
        }

        return ReportsGenerating;
    }
}
