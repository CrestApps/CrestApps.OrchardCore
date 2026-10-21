using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.Documents;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// With the CRM on and the content import feature off, saving an activity load failed without showing an error: the
/// lead import filter looked up the imports still being indexed in the import history, whose tables only that feature
/// creates, and the failed query rolled the save back. These tests pin which provider each feature registers.
/// </summary>
public sealed class LeadImportProviderRegistrationTests
{
    private static readonly DateTime _july = new(2020, 7, 3, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _august = new(2020, 8, 10, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CrmWithoutContentTransfer_ListsTheImportsWithoutReadingTheImportHistory()
    {
        // Arrange
        // The backfill never runs without the import feature, so it never completes; the import history table is
        // missing, as on a tenant that never enabled the feature.
        var databasePath = LeadTestStore.DatabasePath("registration-no-transfer");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("august", "August2020.csv", _august));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var session = store.CreateSession();
            await using var serviceProvider = CreateServices(session, contentTransfer: false);

            // Act
            var imports = await serviceProvider.GetRequiredService<LeadImportProvider>().GetImportsAsync();

            // Assert
            var import = Assert.Single(imports);
            Assert.Equal("august", import.EntryId);
            Assert.False(import.IsIndexing);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task CrmWithContentTransfer_ListsTheImportsTheBackfillHasNotReached()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("registration-transfer");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            store.RegisterIndexes([new ContentTransferEntryIndexProvider()]);

            await using (var schemaSession = store.CreateSession())
            {
                var transaction = await schemaSession.BeginTransactionAsync(TestContext.Current.CancellationToken);

                await new SchemaBuilder(store.Configuration, transaction).CreateMapIndexTableAsync<ContentTransferEntryIndex>(table => table
                    .Column<string>("EntryId", column => column.WithLength(26))
                    .Column<string>("Status", column => column.NotNull().WithLength(25))
                    .Column<ContentTransferDirection>("Direction")
                    .Column<DateTime>("CreatedUtc", column => column.NotNull())
                    .Column<string>("ContentType", column => column.WithLength(255))
                    .Column<string>("Owner", column => column.WithLength(26)));

                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("august", "August2020.csv", _august));
                await seedSession.SaveAsync(
                    new ContentTransferEntry
                    {
                        EntryId = "july",
                        ContentType = LeadTestStore.LeadContentType,
                        UploadedFileName = "July2020.csv",
                        CreatedUtc = _july,
                        ProcessSaveUtc = _july.AddMinutes(5),
                        Status = ContentTransferEntryStatus.Completed,
                        Direction = ContentTransferDirection.Import,
                    },
                    cancellationToken: TestContext.Current.CancellationToken);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var session = store.CreateSession();
            await using var serviceProvider = CreateServices(session, contentTransfer: true);

            // Act
            var imports = await serviceProvider.GetRequiredService<LeadImportProvider>().GetImportsAsync();

            // Assert
            Assert.Equal(["august", "july"], imports.Select(import => import.EntryId));
            Assert.True(imports[1].IsIndexing);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static ServiceProvider CreateServices(ISession session, bool contentTransfer)
    {
        var services = new ServiceCollection();

        services.AddOptions();
        services.AddLogging();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddScoped(_ => session);

        var localClock = new Mock<ILocalClock>();
        localClock
            .Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((DateTimeOffset value) => value);
        services.AddSingleton(localClock.Object);

        // A tenant always has both, whether or not the import feature is on.
        var state = new Mock<IDocumentManager<OmnichannelBackfillState>>();
        state.Setup(manager => manager.GetOrCreateImmutableAsync(It.IsAny<Func<Task<OmnichannelBackfillState>>>()))
            .ReturnsAsync(new OmnichannelBackfillState());
        services.AddSingleton(state.Object);

        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.ListTypeDefinitionsAsync())
            .ReturnsAsync([new ContentTypeDefinitionBuilder()
                .WithName(LeadTestStore.LeadContentType)
                .WithPart(OmnichannelConstants.ContentParts.Lead)
                .Build()]);
        services.AddSingleton(definitions.Object);

        new CrmStartup(new PassThroughStringLocalizer<CrmStartup>()).ConfigureServices(services);

        if (contentTransfer)
        {
            new CrmContentTransferStartup().ConfigureServices(services);
        }

        return services.BuildServiceProvider();
    }

    private static LeadImport Import(string entryId, string fileName, DateTime importedUtc)
        => new()
        {
            EntryId = entryId,
            FileName = fileName,
            ImportedUtc = importedUtc,
        };
}
