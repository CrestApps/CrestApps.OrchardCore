using System.Text;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Reports;

/// <summary>
/// A supervisor who wanted the numbers a campaign dialed, and how each one ended, had only counts per disposition to
/// look at. These run the Dialer call detail report over a real store: one row per dialed activity, several
/// dispositions picked together, pages read in SQL rather than every row, and an export that gets every row.
/// </summary>
public sealed class DialerCallDetailReportProviderTests
{
    private const string CampaignId = "campaign-renewal";
    private const string NoAnswerId = "disposition-no-answer";
    private const string BusyId = "disposition-busy";
    private const string SoldId = "disposition-sold";
    private const string ContactId = "contact-john-smith";
    private const string AgentUserId = "user-agent";

    private static readonly DateTime _from = new(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _to = new(2026, 9, 24, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime _dialedUtc = new(2026, 9, 24, 21, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunAsync_ListsOneRowPerDialedActivity_WithTheDocumentedColumns()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            await using (var seed = store.CreateSession())
            {
                await seed.SaveAsync(
                    new ContentItem
                    {
                        ContentItemId = ContactId,
                        ContentItemVersionId = IdGenerator.GenerateId(),
                        ContentType = "Contact",
                        DisplayText = "John Smith",
                        Latest = true,
                        Published = true,
                    },
                    cancellationToken: cancellationToken);
                await seed.SaveAsync(
                    new User
                    {
                        UserId = AgentUserId,
                        UserName = "agent.smith",
                        NormalizedUserName = "AGENT.SMITH",
                    },
                    cancellationToken: cancellationToken);
                await SaveActivityAsync(seed, "+15555550100", cancellationToken, activity =>
                {
                    activity.ContactContentItemId = ContactId;
                    activity.CampaignId = CampaignId;
                    activity.DispositionId = NoAnswerId;
                    activity.Attempts = 2;
                    activity.AssignedToId = AgentUserId;
                    activity.CompletedUtc = _dialedUtc.AddMinutes(5);
                });
                await SaveActivityAsync(seed, "+15555550101", cancellationToken, activity =>
                {
                    activity.Status = ActivityStatus.Scheduled;
                    activity.CompletedUtc = null;
                    activity.DispositionId = null;
                });
                await seed.SaveChangesAsync(cancellationToken);
            }

            await using var session = store.CreateSession();

            // Act
            var document = await RunAsync(session, cancellationToken);

            // Assert
            var section = Assert.Single(document.Sections);
            Assert.Equal(
                ["Phone number", "Contact", "Campaign", "Last disposition", "Attempts", "Completed (UTC)", "Agent", "Status"],
                section.Columns.Select(column => column.Label));
            Assert.Equal(2, section.Rows.Count);

            // Newest first: the activity saved last leads.
            Assert.Equal(
                ["+15555550101", "(No contact)", "(No campaign)", "(No disposition)", "1", "", "(Unassigned)", "Scheduled"],
                section.Rows[0].Cells);
            Assert.Equal(
                [
                    "+15555550100",
                    "John Smith",
                    "Spring renewal",
                    "No answer",
                    "2",
                    "2026-09-24 21:05:00Z",
                    ReportValue.UserDisplayName("agent.smith", "(Unknown user)"),
                    "Completed",
                ],
                section.Rows[1].Cells);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_WithSeveralDispositionsPicked_KeepsOnlyTheActivitiesThatEndedWithOneOfThem()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            await using (var seed = store.CreateSession())
            {
                await SaveActivityAsync(seed, "+15555550001", cancellationToken, activity => activity.DispositionId = NoAnswerId);
                await SaveActivityAsync(seed, "+15555550002", cancellationToken, activity => activity.DispositionId = BusyId);
                await SaveActivityAsync(seed, "+15555550003", cancellationToken, activity => activity.DispositionId = SoldId);
                await SaveActivityAsync(seed, "+15555550004", cancellationToken, activity => activity.DispositionId = null);
                await seed.SaveChangesAsync(cancellationToken);
            }

            await using var session = store.CreateSession();

            // Act
            var document = await RunAsync(
                session,
                cancellationToken,
                filter => filter.Set<string[]>(OmnichannelReportFilter.DispositionIds, [NoAnswerId, BusyId]));

            // Assert
            var section = Assert.Single(document.Sections);
            Assert.Equal(["Busy", "No answer"], section.Rows.Select(row => row.Cells[3]).Order(StringComparer.Ordinal));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_WithADispositionNoLongerInTheCatalog_SaysItIsUnknownInsteadOfShowingItsId()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            await using (var seed = store.CreateSession())
            {
                await SaveActivityAsync(seed, "+15555550001", cancellationToken, activity => activity.DispositionId = "disposition-deleted");
                await seed.SaveChangesAsync(cancellationToken);
            }

            await using var session = store.CreateSession();

            // Act
            var document = await RunAsync(session, cancellationToken);

            // Assert
            var row = Assert.Single(Assert.Single(document.Sections).Rows);
            Assert.Equal("(Unknown disposition)", row.Cells[3]);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_ReadsOnePageInSql_AndClampsThePageToTheLastOne()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);
        var total = (DialerCallDetailReportProvider.PageSize * 2) + 50;

        try
        {
            await SeedNumberedActivitiesAsync(store, total, cancellationToken);
            await using var session = store.CreateSession();

            // Act
            var firstPage = await RunAsync(session, cancellationToken);
            var lastPage = await RunAsync(session, cancellationToken, filter => filter.Set(OmnichannelReportFilter.Page, 3));
            var beyondTheLastPage = await RunAsync(session, cancellationToken, filter => filter.Set(OmnichannelReportFilter.Page, 9));

            // Assert
            var first = Assert.Single(firstPage.Sections);
            Assert.Equal(DialerCallDetailReportProvider.PageSize, first.Rows.Count);
            Assert.Equal("Page 1 of 3: rows 1 to 100 of 250, newest first. An export includes every row.", first.Description);
            Assert.Equal(NumberedPhone(total), first.Rows[0].Cells[0]);
            Assert.Equal(NumberedPhone(total - DialerCallDetailReportProvider.PageSize + 1), first.Rows[first.Rows.Count - 1].Cells[0]);

            var last = Assert.Single(lastPage.Sections);
            Assert.Equal(50, last.Rows.Count);
            Assert.Equal("Page 3 of 3: rows 201 to 250 of 250, newest first. An export includes every row.", last.Description);
            Assert.Equal(NumberedPhone(1), last.Rows[last.Rows.Count - 1].Cells[0]);

            var clamped = Assert.Single(beyondTheLastPage.Sections);
            Assert.Equal(last.Description, clamped.Description);
            Assert.Equal(last.Rows.Select(row => row.Cells[0]), clamped.Rows.Select(row => row.Cells[0]));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_ForAnExport_ReturnsEveryRow_ReadingThemInBatches()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);
        var total = (DialerCallDetailReportProvider.ExportBatchSize * 2) + 25;

        try
        {
            await SeedNumberedActivitiesAsync(store, total, cancellationToken);
            await using var session = store.CreateSession();

            // Act
            var document = await RunAsync(
                session,
                cancellationToken,
                filter => filter.Set(OmnichannelReportFilter.Page, 2),
                ReportRunMode.Export);

            // Assert
            var section = Assert.Single(document.Sections);
            Assert.Equal(total, section.Rows.Count);
            Assert.Equal($"{total} rows.", section.Description);
            Assert.Equal(NumberedPhone(total), section.Rows[0].Cells[0]);
            Assert.Equal(NumberedPhone(1), section.Rows[total - 1].Cells[0]);
            Assert.Equal(total, section.Rows.Select(row => row.Cells[0]).Distinct(StringComparer.Ordinal).Count());
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_WithTheDialerDefault_ListsEveryDialerModeAndLeavesManualWorkOut()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            await using (var seed = store.CreateSession())
            {
                await SaveActivityAsync(seed, "+15555550001", cancellationToken, activity => activity.Source = ActivitySources.PowerDial);
                await SaveActivityAsync(seed, "+15555550002", cancellationToken, activity => activity.Source = ActivitySources.PreviewDial);
                await SaveActivityAsync(seed, "+15555550003", cancellationToken, activity => activity.Source = ActivitySources.Manual);
                await seed.SaveChangesAsync(cancellationToken);
            }

            await using var session = store.CreateSession();
            var provider = CreateProvider(session);
            var filter = CreateFilter();

            // Act
            provider.ApplyDefaults(filter);
            var document = await provider.RunAsync(new ReportContext(filter), cancellationToken);

            // Assert
            Assert.Equal(ActivitySources.Dialer, filter.GetOrDefault<string>(OmnichannelReportFilter.Source));
            var section = Assert.Single(document.Sections);
            Assert.Equal(["+15555550001", "+15555550002"], section.Rows.Select(row => row.Cells[0]).Order(StringComparer.Ordinal));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_PlacesCompletedAttemptsOnTheirCompletionDay_AndOpenOnesOnTheirScheduledDay()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            await using (var seed = store.CreateSession())
            {
                // Scheduled the day before, completed in the period: listed.
                await SaveActivityAsync(seed, "+15555550001", cancellationToken, activity => activity.ScheduledUtc = _dialedUtc.AddDays(-1));

                // Scheduled in the period, completed the day after: not listed.
                await SaveActivityAsync(seed, "+15555550002", cancellationToken, activity => activity.CompletedUtc = _dialedUtc.AddDays(1));

                // Being dialed now, scheduled in the period: listed.
                await SaveActivityAsync(seed, "+15555550003", cancellationToken, activity =>
                {
                    activity.Status = ActivityStatus.Dialing;
                    activity.CompletedUtc = null;
                });

                // Waiting for a retry scheduled the day after: not listed.
                await SaveActivityAsync(seed, "+15555550004", cancellationToken, activity =>
                {
                    activity.Status = ActivityStatus.Scheduled;
                    activity.CompletedUtc = null;
                    activity.ScheduledUtc = _dialedUtc.AddDays(1);
                });
                await seed.SaveChangesAsync(cancellationToken);
            }

            await using var session = store.CreateSession();

            // Act
            var document = await RunAsync(session, cancellationToken);

            // Assert
            var section = Assert.Single(document.Sections);
            Assert.Equal(["+15555550001", "+15555550003"], section.Rows.Select(row => row.Cells[0]).Order(StringComparer.Ordinal));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_WithNothingInThePeriod_ReturnsAnEmptyDocument()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            await using var session = store.CreateSession();

            // Act
            var document = await RunAsync(session, cancellationToken);

            // Assert
            Assert.False(document.HasContent);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task CsvExport_HasTheReportColumns_AndOneLinePerRow()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            await SeedNumberedActivitiesAsync(store, 3, cancellationToken);
            await using var session = store.CreateSession();
            var document = await RunAsync(session, cancellationToken, mode: ReportRunMode.Export);
            var format = new CsvReportExportFormat();

            // Act
            var csv = Encoding.UTF8.GetString(format.Serialize(document)).TrimStart('﻿');

            // Assert
            var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("Phone number,Contact,Campaign,Last disposition,Attempts,Completed (UTC),Agent,Status", lines[0]);
            Assert.Equal(4, lines.Length);
            Assert.StartsWith(NumberedPhone(3) + ",", lines[1], StringComparison.Ordinal);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static string NumberedPhone(int number)
        => $"+1555555{number:D4}";

    private static async Task SeedNumberedActivitiesAsync(IStore store, int count, CancellationToken cancellationToken)
    {
        await using var seed = store.CreateSession();

        for (var number = 1; number <= count; number++)
        {
            await SaveActivityAsync(seed, NumberedPhone(number), cancellationToken, activity => activity.DispositionId = NoAnswerId);
        }

        await seed.SaveChangesAsync(cancellationToken);
    }

    private static async Task<ReportDocument> RunAsync(
        ISession session,
        CancellationToken cancellationToken,
        Action<ReportFilter> configure = null,
        ReportRunMode mode = ReportRunMode.Display)
    {
        var filter = CreateFilter();
        configure?.Invoke(filter);

        return await CreateProvider(session).RunAsync(new ReportContext(filter, mode), cancellationToken);
    }

    private static ReportFilter CreateFilter()
    {
        var filter = new ReportFilter
        {
            ReportName = DialerCallDetailReportProvider.ReportName,
        };

        filter.SetDateRange(new ReportDateRange
        {
            FromUtc = _from,
            ToUtc = _to,
        });

        return filter;
    }

    private static DialerCallDetailReportProvider CreateProvider(ISession session)
    {
        var campaignManager = new Mock<ICatalogManager<OmnichannelCampaign>>();
        campaignManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelCampaign[]
            {
                new()
                {
                    ItemId = CampaignId,
                    DisplayText = "Spring renewal",
                },
            });

        var dispositionManager = new Mock<INamedCatalogManager<OmnichannelDisposition>>();
        dispositionManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelDisposition[]
            {
                new()
                {
                    ItemId = NoAnswerId,
                    Name = "No answer",
                },
                new()
                {
                    ItemId = BusyId,
                    Name = "Busy",
                },
                new()
                {
                    ItemId = SoldId,
                    Name = "Sold",
                },
            });

        // The Dialer source stands for every dialer mode, as the Outbound Dialer and Paced Dialing features register it.
        var sourceOptions = new ActivitySourceOptions();
        sourceOptions.AddSource(ActivitySources.Dialer, entry => entry.Matches(
            ActivitySources.PreviewDial,
            ActivitySources.PowerDial,
            ActivitySources.ProgressiveDial,
            ActivitySources.PredictiveDial));
        sourceOptions.AddSource(ActivitySources.Manual);

        return new DialerCallDetailReportProvider(
            session,
            campaignManager.Object,
            dispositionManager.Object,
            Options.Create(sourceOptions),
            new PassThroughStringLocalizer<DialerCallDetailReportProvider>());
    }

    private static async Task SaveActivityAsync(
        ISession session,
        string phoneNumber,
        CancellationToken cancellationToken,
        Action<OmnichannelActivity> configure = null)
    {
        var activity = new OmnichannelActivity
        {
            ItemId = IdGenerator.GenerateId(),
            Source = ActivitySources.PowerDial,
            Channel = OmnichannelConstants.Channels.Phone,
            PreferredDestination = phoneNumber,
            ContactContentType = "Contact",
            SubjectContentType = "Renewal",
            CreatedUtc = _dialedUtc.AddMinutes(-30),
            ScheduledUtc = _dialedUtc,
            CompletedUtc = _dialedUtc.AddMinutes(1),
            Attempts = 1,
            Status = ActivityStatus.Completed,
        };

        configure?.Invoke(activity);

        await session.SaveAsync(activity, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
    }

    private static async Task<(IStore Store, string DatabasePath)> CreateStoreAsync(CancellationToken cancellationToken)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"dialer-call-detail-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new OmnichannelActivityIndexProvider(), new ContentItemIndexProvider(), new TestUserIndexProvider()]);
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

        await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest")
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<DateTime>("ModifiedUtc")
            .Column<DateTime>("PublishedUtc")
            .Column<DateTime>("CreatedUtc")
            .Column<string>("Owner", column => column.WithLength(255))
            .Column<string>("Author", column => column.WithLength(255))
            .Column<string>("DisplayText", column => column.WithLength(255)));

        await schemaBuilder.CreateMapIndexTableAsync<UserIndex>(table => table
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("NormalizedUserName", column => column.WithLength(255))
            .Column<string>("NormalizedEmail", column => column.WithLength(255))
            .Column<bool>("IsEnabled")
            .Column<bool>("IsLockoutEnabled")
            .Column<int>("AccessFailedCount")
            .Column<DateTime>("LockoutEndUtc"));

        await transaction.CommitAsync(cancellationToken);

        return (store, databasePath);
    }

    /// <summary>
    /// Indexes the two user columns the report reads, without the lookup normalizer the real provider needs.
    /// </summary>
    private sealed class TestUserIndexProvider : IndexProvider<User>
    {
        public override void Describe(DescribeContext<User> context)
        {
            context
                .For<UserIndex>()
                .Map(user => new UserIndex
                {
                    UserId = user.UserId,
                    NormalizedUserName = user.NormalizedUserName,
                });
        }
    }
}
