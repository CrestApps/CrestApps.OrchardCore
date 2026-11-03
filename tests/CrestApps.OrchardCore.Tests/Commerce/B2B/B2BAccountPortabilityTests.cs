using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Accounts.B2B.Deployments;
using CrestApps.OrchardCore.Accounts.B2B.Handlers;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Recipes;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Json.Schema;
using Microsoft.Data.Sqlite;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B;

/// <summary>
/// Price lists move between sites by recipe and deployment, buyers are linked to the account's contacts, and an
/// existing order index loses the company column for the account column.
/// </summary>
public sealed class B2BAccountPortabilityTests
{
    [Fact]
    public async Task ThePriceListsStep_CreatesNewLists_AndUpdatesTheOnesItKnows_ByIdThenName()
    {
        // Arrange
        var byId = new PriceList { ItemId = "list-1", Name = "Old name", Currency = "USD" };
        var byName = new PriceList { ItemId = "list-2", Name = "Partners", Currency = "EUR" };
        var created = new List<PriceList>();
        var updated = new List<PriceList>();
        var store = new Mock<IPriceListStore>();
        store.Setup(value => value.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([byId, byName]);
        store.Setup(value => value.CreateAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>())).Callback<PriceList, CancellationToken>((list, _) => created.Add(list)).Returns(ValueTask.CompletedTask);
        store.Setup(value => value.UpdateAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>())).Callback<PriceList, CancellationToken>((list, _) => updated.Add(list)).Returns(ValueTask.CompletedTask);

        var step = new JsonObject
        {
            ["name"] = PriceListsStep.Name,
            ["PriceLists"] = new JsonArray
            {
                new JsonObject { ["ItemId"] = "list-1", ["Name"] = "Wholesale", ["PercentOff"] = 10, ["Currency"] = "USD", ["Entries"] = new JsonArray { new JsonObject { ["Sku"] = "SKU-1", ["UnitPrice"] = 8.5 } } },
                new JsonObject { ["Name"] = "partners", ["Currency"] = "EUR" },
                new JsonObject { ["Name"] = "Distributors", ["PercentOff"] = 5 },
                new JsonObject { ["Currency"] = "USD" },
            },
        };
        var context = new RecipeExecutionContext { Name = PriceListsStep.Name, Step = step };

        // Act
        await new PriceListsStep(store.Object, Mock.Of<IClock>()).ExecuteAsync(context);

        // Assert
        Assert.Equal(["list-1", "list-2"], updated.Select(list => list.ItemId));
        Assert.Equal("Wholesale", byId.Name);
        Assert.Equal(10m, byId.PercentOff);
        Assert.Equal(8.5m, Assert.Single(byId.Entries).UnitPrice);
        Assert.Equal("Distributors", Assert.Single(created).Name);

        // A list without a name is refused, and said so.
        Assert.Single(context.Errors);
    }

    [Fact]
    public async Task ThePriceListsDeploymentStep_ExportsWhatTheRecipeStepAndItsSchemaRead()
    {
        // Arrange
        var store = new Mock<IPriceListStore>();
        store.Setup(value => value.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new PriceList { ItemId = "list-1", Name = "Wholesale", PercentOff = 10m, Currency = "USD", Entries = [new PriceListEntry { Sku = "SKU-1", UnitPrice = 8.5m }] },
        ]);
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        // Act
        await new PriceListsDeploymentSource(store.Object).ProcessDeploymentStepAsync(new PriceListsDeploymentStep(), result);
        var exported = JsonNode.Parse(Assert.Single(result.Steps).ToJsonString())!.AsObject();
        var schema = await new PriceListsRecipeStepSchema().GetSchemaAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PriceListsStep.Name, exported["name"]!.GetValue<string>());
        Assert.Equal("SKU-1", exported["PriceLists"]![0]!["Entries"]![0]!["Sku"]!.GetValue<string>());
        Assert.True(Evaluate(schema, exported));
        Assert.False(Evaluate(schema, new JsonObject { ["name"] = PriceListsStep.Name, ["PriceLists"] = new JsonArray { new JsonObject { ["Currency"] = "USD" } } }));
    }

    [Fact]
    public async Task SavingAnAccount_LinksEachBuyerToTheContactWithTheirEmail()
    {
        // Arrange
        var contentItem = TestB2BAccounts.CreateItem(configure: part => part.Buyers =
        [
            new AccountBuyer { UserId = "user-1", Email = "ada@contoso.example" },
            new AccountBuyer { UserId = "user-2", Email = "bob@contoso.example", ContactContentItemId = "contact-gone" },
        ]);
        var finder = new Mock<IAccountContactFinder>();
        finder.Setup(value => value.FindContactByEmailAsync("account-1", "ada@contoso.example", It.IsAny<CancellationToken>())).ReturnsAsync("contact-1");
        var handler = new B2BAccountPartHandler([finder.Object]);
        contentItem.TryGet<B2BAccountPart>(out var part);

        // Act
        await handler.UpdatingAsync(new UpdateContentContext(contentItem), part);

        // Assert
        var buyers = new B2BAccount(contentItem).Buyers;

        Assert.Equal("contact-1", buyers[0].ContactContentItemId);
        Assert.Null(buyers[1].ContactContentItemId);
    }

    [Fact]
    public async Task TheOrdersMigration_ReplacesTheCompanyColumnWithTheAccountColumn()
    {
        // Arrange: an order index made before accounts, with its company column and index.
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-orders-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={path};Pooling=False"));

        try
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.InitializeCollectionAsync(OrderConstants.CollectionName, TestContext.Current.CancellationToken);

            await using (var session = store.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

                await schemaBuilder.CreateMapIndexTableAsync<OrderIndex>(table => table
                    .Column<string>("ItemId", column => column.WithLength(26))
                    .Column<string>("CompanyId", column => column.Nullable().WithLength(26)),
                    collection: OrderConstants.CollectionName);
                await schemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
                    .CreateIndex("IDX_OrderIndex_CompanyId", "CompanyId"),
                    collection: OrderConstants.CollectionName);

                // Act
                await new OrderMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom2Async();
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            var columns = new List<string>();

            await using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);

                await using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA table_info('{OrderConstants.CollectionName}_{nameof(OrderIndex)}')";

                await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

                while (await reader.ReadAsync(TestContext.Current.CancellationToken))
                {
                    columns.Add(reader.GetString(1));
                }
            }

            Assert.Contains("AccountId", columns);
            Assert.DoesNotContain("CompanyId", columns);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    private static bool Evaluate(JsonSchema schema, JsonObject step)
    {
        using var document = JsonDocument.Parse(step.ToJsonString());

        return schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Flag }).IsValid;
    }
}
