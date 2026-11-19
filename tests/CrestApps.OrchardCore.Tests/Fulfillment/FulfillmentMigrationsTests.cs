using CrestApps.OrchardCore.Fulfillment;
using CrestApps.OrchardCore.Fulfillment.Core.Indexes;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Migrations;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Fulfillment;

/// <summary>
/// Verifies upgrades from both index schemas installed as version one of Fulfillment.
/// </summary>
public sealed class FulfillmentMigrationsTests
{
    /// <summary>
    /// Verifies that a previously accepted request stays sent and can be found by its order number.
    /// </summary>
    /// <param name="legacySchema">Whether the version-one index lacks the order number and shipment columns.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateFrom1Async_EitherVersionOneSchema_KeepsSentRequestsSearchable(bool legacySchema)
    {
        // Arrange
        await using var harness = new FulfillmentTestHarness();
        await harness.InitializeAsync();

        var token = FulfillmentTestHarness.Token;
        var request = harness.NewRequest("existing-request", "partner");
        request.Status = FulfillmentRequestStatus.Sent;
        request.ExternalOrderId = "accepted-order";

        await using (var session = harness.Store.CreateSession())
        {
            await new FulfillmentRequestStore(session).CreateAsync(request, token);
            await session.SaveChangesAsync(token);
        }

        await using (var session = harness.Store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(token);
            var schema = new SchemaBuilder(harness.Store.Configuration, transaction);

            await using (var command = transaction.Connection.CreateCommand())
            {
                command.Transaction = transaction;
                // The earlier table, or a retry after the columns exist but their data has not been filled.
                command.CommandText = legacySchema
                    ? """
                      DROP INDEX IDX_FulfillmentRequestIndex_List;
                      ALTER TABLE Fulfillment_FulfillmentRequestIndex DROP COLUMN OrderNumber;
                      ALTER TABLE Fulfillment_FulfillmentRequestIndex DROP COLUMN ShipmentId;
                      """
                    : "UPDATE Fulfillment_FulfillmentRequestIndex SET OrderNumber = NULL";
                await command.ExecuteNonQueryAsync(token);
            }

            // Act
            var migration = new FulfillmentMigrations(session) { SchemaBuilder = schema };
            Assert.Equal(2, await migration.UpdateFrom1Async());
            Assert.True(await IndexTableIndexCreation.ExistsAsync(schema, typeof(FulfillmentRequestIndex), "IDX_FulfillmentRequestIndex_List", FulfillmentConstants.CollectionName));
            await session.SaveChangesAsync(token);
        }

        // Assert
        await using var reading = harness.Store.CreateSession();
        var (items, total) = await new FulfillmentRequestStore(reading).ListPageAsync(FulfillmentRequestStatus.Sent, request.OrderNumber, 0, 10, token);
        var saved = Assert.Single(items);

        Assert.Equal(1, total);
        Assert.Equal(request.ItemId, saved.ItemId);
        Assert.Equal("accepted-order", saved.ExternalOrderId);
    }
}
