using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Moq;
using OrchardCore;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Documents;
using OrchardCore.Flows.Models;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// A SQLite store with the content item index, the contact index and the phone number index, built by the module's own
/// providers and migration, so lookups by number run against the rows and schema a tenant actually has.
/// </summary>
internal static class ContactPhoneTestStore
{
    public const string ContactContentType = "Contact";
    public const string LeadContentType = "Lead";

    /// <summary>
    /// Creates the store and its index tables.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="withPhoneNumberIndex">Whether the phone number index is written, as it is after the upgrade;
    /// <see langword="false"/> saves records the way they were saved before it.</param>
    public static async Task<IStore> CreateAsync(string connectionString, bool withPhoneNumberIndex = true)
    {
        var store = await OpenAsync(connectionString, withPhoneNumberIndex);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

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

        await OmnichannelContactsMigrations.CreateContactIndexTableAsync(schemaBuilder);
        await OmnichannelContactsMigrations.CreatePhoneNumberIndexTableAsync(schemaBuilder);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    /// <summary>
    /// Opens a store over a database whose tables already exist, as a tenant does after an upgrade.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="withPhoneNumberIndex">Whether the phone number index is written.</param>
    public static async Task<IStore> OpenAsync(string connectionString, bool withPhoneNumberIndex = true)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite(connectionString));

        var providers = new List<IIndexProvider>
        {
            new ContentItemIndexProvider(),
            new OmnichannelContactIndexProvider(new DefaultPhoneNumberService(), TestContactMethods.Accessor),
        };

        if (withPhoneNumberIndex)
        {
            providers.Add(CreatePhoneNumberIndexProvider());
        }

        store.RegisterIndexes(providers);

        await store.InitializeAsync(TestContext.Current.CancellationToken);

        return store;
    }

    /// <summary>
    /// Creates the phone number index provider the module registers.
    /// </summary>
    public static IIndexProvider CreatePhoneNumberIndexProvider()
        => new OmnichannelContactPhoneNumberIndexProvider(new DefaultPhoneNumberService(), TestContactMethods.Accessor);

    /// <summary>
    /// Creates the lookup the module registers.
    /// </summary>
    /// <param name="session">The session the lookup reads.</param>
    /// <param name="backfilled">Whether the phone number backfill has finished.</param>
    public static IOmnichannelContactPhoneLookup CreateLookup(ISession session, bool backfilled = true)
        => new OmnichannelContactPhoneLookup(session, CreateBackfillState(backfilled));

    /// <summary>
    /// Creates a lookup that finds no record at any number.
    /// </summary>
    public static IOmnichannelContactPhoneLookup CreateEmptyLookup()
    {
        var lookup = new Mock<IOmnichannelContactPhoneLookup>();

        lookup
            .Setup(l => l.FindByNumbersAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<OmnichannelContactVersion>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        lookup
            .Setup(l => l.GetNumbersAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<OmnichannelContactVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return lookup.Object;
    }

    /// <summary>
    /// Creates a backfill state document manager reporting whether the phone number backfill has finished.
    /// </summary>
    /// <param name="backfilled">Whether the phone number backfill has finished.</param>
    public static IDocumentManager<OmnichannelBackfillState> CreateBackfillState(bool backfilled)
    {
        var state = new OmnichannelBackfillState();

        if (backfilled)
        {
            state.Completed.Add(global::CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks.ContactPhoneNumberIndexBackfillBackgroundTask.BackfillName);
        }

        var manager = new Mock<IDocumentManager<OmnichannelBackfillState>>();
        manager
            .Setup(documents => documents.GetOrCreateImmutableAsync(It.IsAny<Func<Task<OmnichannelBackfillState>>>()))
            .ReturnsAsync(state);

        return manager.Object;
    }

    /// <summary>
    /// Builds a published contact or lead with the given numbers, each as <c>(type, number)</c>.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <param name="numbers">The numbers, in the order they are saved.</param>
    public static ContentItem CreateRecord(string contentType, params (string Type, string Number)[] numbers)
    {
        var record = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            ContentType = contentType,
            DisplayText = contentType,
            Published = true,
            Latest = true,
        };

        record.Alter<OmnichannelContactPart>(_ => { });

        var bagPart = new BagPart();

        foreach (var (type, number) in numbers)
        {
            var method = new ContentItem
            {
                ContentItemId = IdGenerator.GenerateId(),
                ContentType = OmnichannelConstants.ContentTypes.PhoneNumber,
            };

            method.Alter<PhoneNumberInfoPart>(part =>
            {
                part.Number = new PhoneField
                {
                    PhoneNumber = number,
                };
                part.Type = new TextField
                {
                    Text = type,
                };
            });

            bagPart.ContentItems.Add(method);
        }

        record.Apply(OmnichannelConstants.NamedParts.ContactMethods, bagPart);

        return record;
    }

    /// <summary>
    /// Saves records in one committed session.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="records">The records to save.</param>
    public static async Task SaveAsync(IStore store, params ContentItem[] records)
    {
        await using var session = store.CreateSession();

        foreach (var record in records)
        {
            await session.SaveAsync(record, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Returns a SQLite connection string for a new database file, and its path.
    /// </summary>
    /// <param name="name">A short name for the database file.</param>
    public static (string ConnectionString, string Path) CreateDatabase(string name)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.db");

        return ($"Data Source={path};Pooling=False", path);
    }
}
