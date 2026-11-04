using CrestApps.OrchardCore.Classifieds.Indexes;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.Classifieds.Services;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// The listings a person posted are found by their account; erasing takes them down and removes where they were offered.
/// </summary>
public sealed class ClassifiedPersonalDataContributorTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-pd-classifieds-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new ClassifiedIndexProvider()]);
        await _store.InitializeAsync(cancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        // The columns of the index table the Classifieds migration creates.
        await new SchemaBuilder(_store.Configuration, transaction).CreateMapIndexTableAsync<ClassifiedIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("Owner", column => column.Nullable().WithLength(255))
            .Column<string>("DisplayText", column => column.Nullable().WithLength(255))
            .Column<int>("Status")
            .Column<DateTime>("ExpiresUtc", column => column.Nullable())
            .Column<DateTime>("FeaturedUntilUtc", column => column.Nullable())
            .Column<DateTime>("SortUtc", column => column.Nullable())
            .Column<double>("Latitude", column => column.Nullable())
            .Column<double>("Longitude", column => column.Nullable())
            .Column<bool>("Published")
            .Column<bool>("Latest"));

        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task APostersListings_AreExported_AndTakenDown()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(Listing("l-mine", "user-1"), cancellationToken: cancellationToken);
            await session.SaveAsync(Listing("l-other", "user-2"), cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        var contentManager = new Mock<IContentManager>();
        var updated = new List<ContentItem>();
        contentManager.Setup(manager => manager.UpdateAsync(It.IsAny<ContentItem>())).Callback<ContentItem>(updated.Add).Returns(Task.CompletedTask);

        var subject = new PersonalDataSubject(userId: "user-1");
        var export = new PersonalDataExport();
        var result = new PersonalDataAnonymizeResult();

        // Act
        await using (var session = _store.CreateSession())
        {
            var contributor = new ClassifiedPersonalDataContributor(session, contentManager.Object);

            await contributor.ExportAsync(new PersonalDataExportContext(subject, export), cancellationToken);
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), cancellationToken);
        }

        // Assert
        var record = Assert.Single(export.Records);
        Assert.Equal("l-mine", record.Id);
        Assert.Equal("Main Street", record.Fields[nameof(ClassifiedPart.Place)]);

        var listing = Assert.Single(updated);
        Assert.True(listing.TryGet<ClassifiedPart>(out var part));
        Assert.Equal(ClassifiedStatus.Expired, part.Status);
        Assert.Null(part.Place);
        Assert.Null(part.Latitude);
        contentManager.Verify(manager => manager.UnpublishAsync(listing), Times.Once);
    }

    private static ContentItem Listing(string id, string owner)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = "Listing",
            DisplayText = "Bike for sale",
            Owner = owner,
            Published = true,
            Latest = true,
        };

        item.Apply(new ClassifiedPart
        {
            Status = ClassifiedStatus.Approved,
            Place = "Main Street",
            Latitude = 36.1,
            Longitude = -115.1,
        });

        return item;
    }
}
