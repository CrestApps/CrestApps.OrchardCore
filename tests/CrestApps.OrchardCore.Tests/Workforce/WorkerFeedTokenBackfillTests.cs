using CrestApps.Core;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// SEC-P10 i, second review: feed secrets stored before they were hashed get their hash and protected copy. Bug: on an
/// upgrade from a build before Staff, the portal's one-time backfill ran on the start that turned Staff on, before the
/// workers moved to the Staff collection, found none and never ran again; the workers then moved with their plain
/// secret only. The backfill also read every worker at once and committed once.
/// </summary>
public sealed class WorkerFeedTokenBackfillTests
{
    private const string Legacy = "legacy-plain-feed-secret-0123456789abcdef";

    [Fact]
    public async Task AWorkerMovedToStaff_GetsTheHashOfItsPlainFeedSecret_AndItsAddressStillOpens()
    {
        // Arrange: a worker document of the build before Staff, with its feed secret in plain text.
        await using var harness = await CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var content = "{\"ItemId\":\"w-legacy\",\"DisplayName\":\"Ana\",\"Properties\":{\"WorkerPortalSettings\":{\"FeedToken\":\"" + Legacy + "\"}}}";

        // Act: the Workforce migration's move, on the migration's own session and transaction.
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<global::YesSql.ISession>();
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(harness.Store.Configuration, transaction);
            var table = schemaBuilder.Dialect.QuoteForTableName(
                schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetDocumentTable(WorkforceConstants.CollectionName),
                harness.Store.Configuration.Schema);

            await using (var insert = schemaBuilder.Connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = $"INSERT INTO {table} (Id, Type, Content, Version) VALUES (900001, @type, @content, 1)";
                AddParameter(insert, "@type", WorkforceWorkersMove.LegacyTypeName);
                AddParameter(insert, "@content", content);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await WorkforceWorkersMove.MoveAsync(schemaBuilder, harness.Store, session, services.GetRequiredService<IDataProtectionProvider>());
        });

        // Assert
        var settings = await ReadSettingsAsync(harness, "w-legacy");
        var feed = await harness.RunAsync(services => services.GetRequiredService<IWorkerFeedService>().WriteAsync(Legacy), commit: false);

        Assert.Equal(PortalSecrets.Hash(Legacy), settings.FeedTokenHash);
        Assert.False(string.IsNullOrEmpty(settings.ProtectedFeedToken));
        Assert.NotNull(feed);
    }

    [Fact]
    public async Task ShowingAPlainFeedSecret_GivesItItsHashAndProtectedCopy()
    {
        // Arrange: a worker the backfill never reached.
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team, worker => worker.Put(new WorkerPortalSettings { FeedToken = Legacy }));

        // Act
        var shown = await harness.RunAsync(async services => await services.GetRequiredService<IWorkerFeedService>().GetTokenAsync(await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(ana.ItemId), renew: false));

        // Assert: the same address, now stored hashed.
        var settings = await ReadSettingsAsync(harness, ana.ItemId);

        Assert.Equal(Legacy, shown);
        Assert.Equal(PortalSecrets.Hash(Legacy), settings.FeedTokenHash);
        Assert.False(string.IsNullOrEmpty(settings.ProtectedFeedToken));
    }

    [Fact]
    public async Task TheBackfill_ReadsAndCommitsAPageAtATime_ReachesEveryWorker_AndCanRunAgain()
    {
        // Arrange: more workers than one page, one of them already hashed.
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ids = new List<string>();

        for (var i = 0; i < 5; i++)
        {
            var token = $"{Legacy}-{i}";
            var worker = await PlanningKit.AddMemberAsync(harness, "Worker " + i, team, member => member.Put(new WorkerPortalSettings { FeedToken = token }));

            ids.Add(worker.ItemId);
        }

        await harness.RunAsync(async services => await services.GetRequiredService<IWorkerFeedService>().GetTokenAsync(await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(ids[0]), renew: true));

        // Act
        var first = await harness.RunAsync(services => RunBackfillAsync(services, batchSize: 2));
        var second = await harness.RunAsync(services => RunBackfillAsync(services, batchSize: 2));

        // Assert
        Assert.Equal(4, first);
        Assert.Equal(0, second);

        for (var i = 1; i < 5; i++)
        {
            var settings = await ReadSettingsAsync(harness, ids[i]);

            Assert.Equal(PortalSecrets.Hash($"{Legacy}-{i}"), settings.FeedTokenHash);
            Assert.False(string.IsNullOrEmpty(settings.ProtectedFeedToken));
        }
    }

    private static Task<int> RunBackfillAsync(IServiceProvider services, int batchSize)
    {
        var backfill = services.GetRequiredService<WorkerFeedTokenBackfill>();

        backfill.BatchSize = batchSize;

        return backfill.RunAsync();
    }

    private static Task<WorkerPortalSettings> ReadSettingsAsync(CalendarTestHarness harness, string workerId)
    {
        return harness.RunAsync(
            async services => (await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(workerId)).TryGet<WorkerPortalSettings>(out var settings) ? settings : null,
            commit: false);
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();

        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return WorkforceTests.CreateAsync(services =>
        {
            services.AddDistributedMemoryCache();
            services.AddWorkforceTimeOffCore();
            services.AddWorkforcePortalCore();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        });
    }
}
