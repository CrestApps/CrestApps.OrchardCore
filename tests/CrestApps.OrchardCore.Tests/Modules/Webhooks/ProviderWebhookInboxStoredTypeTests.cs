using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using YesSql;
using YesSql.Provider.Sqlite;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks;

/// <summary>
/// The inbox record moved from Contact Center to the webhooks building block. YesSql finds documents by their stored
/// type name, so the record must keep writing and reading the name it was first stored under, or every delivery a
/// tenant already holds disappears on upgrade.
/// </summary>
public sealed class ProviderWebhookInboxStoredTypeTests
{
    private const string LegacyTypeName = "CrestApps.OrchardCore.ContactCenter.Core.Models.ProviderWebhookInboxMessage, CrestApps.OrchardCore.ContactCenter.Core";

    [Fact]
    public async Task Message_IsStoredUnderItsLegacyTypeName_AndReadBackByIt()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"webhook-inbox-type-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ProviderWebhookInboxStorage.CollectionName, TestContext.Current.CancellationToken);

        try
        {
            await using (var session = store.CreateSession())
            {
                await new ProviderWebhookInboxStore(session).CreateAsync(new ProviderWebhookInboxMessage
                {
                    ItemId = "message-1",
                    ProviderName = "provider",
                    DeliveryId = "delivery-1",
                    HandlerName = "handler",
                    Payload = "{}",
                }, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            string storedType;

            await using (var connection = store.Configuration.ConnectionFactory.CreateConnection())
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT \"Type\" FROM \"{ProviderWebhookInboxStorage.CollectionName}_Document\"";
                storedType = (string)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            }

            ProviderWebhookInboxMessage readBack;

            await using (var session = store.CreateSession())
            {
                readBack = await session.Query<ProviderWebhookInboxMessage>(collection: ProviderWebhookInboxStorage.CollectionName)
                    .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            Assert.Equal(LegacyTypeName, storedType);
            Assert.Equal(LegacyTypeName, ProviderWebhookInboxMessage.StoredTypeName);
            Assert.NotNull(readBack);
            Assert.Equal("delivery-1", readBack.DeliveryId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public void Collection_KeepsTheContactCenterCollectionTheRowsAlreadyLiveIn()
    {
        Assert.Equal("ContactCenter", ProviderWebhookInboxStorage.CollectionName);
    }
}
