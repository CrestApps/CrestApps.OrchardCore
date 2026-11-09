using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Deployments;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Recipes;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Deployment;
using OrchardCore.Lists.Indexes;
using OrchardCore.Lists.Models;
using OrchardCore.Recipes.Models;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Accounts;

/// <summary>
/// Accounts move between sites with the Accounts deployment and recipe steps: every account with its parts, and the
/// records it holds.
/// </summary>
public sealed class AccountPortabilityTests
{
    [Fact]
    public async Task TheAccountsStep_ImportsTheAccountsBeforeTheirRecords_AndRefusesAnItemThatIsNotAnAccount()
    {
        // Arrange
        var imported = new List<string[]>();
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.ImportAsync(It.IsAny<IEnumerable<ContentItem>>()))
            .Callback<IEnumerable<ContentItem>>(items => imported.Add([.. items.Select(item => item.ContentItemId)]))
            .Returns(Task.CompletedTask);

        var step = new JsonObject
        {
            ["name"] = AccountsStep.Name,
            ["Accounts"] = new JsonArray
            {
                JObject.FromObject(Account("account-1")),
                JObject.FromObject(new ContentItem { ContentItemId = "not-an-account", ContentItemVersionId = "v", ContentType = "Article" }),
            },
            ["Records"] = new JsonArray
            {
                JObject.FromObject(Contact("contact-1", "account-1")),
                new JsonObject { ["DisplayText"] = "No id" },
            },
        };
        var context = new RecipeExecutionContext { Name = AccountsStep.Name, Step = step };

        // Act
        await new AccountsStep(contentManager.Object).ExecuteAsync(context);

        // Assert
        Assert.Equal(2, imported.Count);
        Assert.Equal(["account-1"], imported[0]);
        Assert.Equal(["contact-1"], imported[1]);
        Assert.Equal(2, context.Errors.Count);
        Assert.Contains(context.Errors, error => error.Contains("not-an-account", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheAccountsDeploymentStep_ExportsEveryAccount_AndTheRecordsTheyHoldWhenAsked()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-accounts-deploy-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);

        try
        {
            await using (var session = store.CreateSession())
            {
                await session.SaveAsync(Account("account-1", new AccountMember { UserId = "user-1" }), cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveAsync(Contact("contact-1", "account-1"), cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveAsync(Contact("contact-elsewhere", "another-list"), cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var definitions = new InMemoryContentDefinitionManager();
            await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Account").WithPart(AccountsConstants.ContentParts.Account).Build());
            var provider = new AccountContentTypeProvider(definitions, []);

            // Act
            JsonObject withRecords;
            JsonObject withoutRecords;

            await using (var session = store.CreateSession())
            {
                withRecords = await ExportAsync(new AccountsDeploymentSource(session, provider), new AccountsDeploymentStep());
                withoutRecords = await ExportAsync(new AccountsDeploymentSource(session, provider), new AccountsDeploymentStep { IncludeRecords = false });
            }

            // Assert
            Assert.Equal(AccountsStep.Name, withRecords["name"]!.GetValue<string>());
            var account = Assert.Single(withRecords["Accounts"]!.AsArray())!;
            Assert.Equal("account-1", account["ContentItemId"]!.GetValue<string>());
            Assert.Equal("user-1", account["AccountMembersPart"]!["Members"]![0]!["UserId"]!.GetValue<string>());
            Assert.Equal("contact-1", Assert.Single(withRecords["Records"]!.AsArray())!["ContentItemId"]!.GetValue<string>());

            Assert.Single(withoutRecords["Accounts"]!.AsArray());
            Assert.Null(withoutRecords["Records"]);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task TheAccountsDeploymentStep_ReadsTheAccountsAPageAtATime_AndExportsThemAll()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-accounts-pages-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);

        try
        {
            await using (var session = store.CreateSession())
            {
                for (var index = 1; index <= 5; index++)
                {
                    await session.SaveAsync(Account($"account-{index}"), cancellationToken: TestContext.Current.CancellationToken);
                    await session.SaveAsync(Contact($"contact-{index}", $"account-{index}"), cancellationToken: TestContext.Current.CancellationToken);
                }

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var definitions = new InMemoryContentDefinitionManager();
            await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Account").WithPart(AccountsConstants.ContentParts.Account).Build());
            var provider = new AccountContentTypeProvider(definitions, []);

            // Act
            JsonObject exported;

            await using (var session = store.CreateSession())
            {
                exported = await ExportAsync(new AccountsDeploymentSource(session, provider) { BatchSize = 2 }, new AccountsDeploymentStep());
            }

            // Assert
            Assert.Equal(
                ["account-1", "account-2", "account-3", "account-4", "account-5"],
                exported["Accounts"]!.AsArray().Select(account => account!["ContentItemId"]!.GetValue<string>()));
            Assert.Equal(5, exported["Records"]!.AsArray().Count);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public void AccountPageTabs_PlaceEachTabInTheSameOrder_WhicheverModuleAddsIt()
    {
        Assert.Equal("Content:1#Overview;0", AccountPageTabs.Location(AccountPageTabs.Overview));
        Assert.Equal("Content:3#Orders;50", AccountPageTabs.Location(AccountPageTabs.Orders, "3"));
        Assert.Equal("Content:1#Warranty;90", AccountPageTabs.Location("Warranty"));
        Assert.Equal("Parts:1#Members;5", AccountPageTabs.EditorLocation(AccountPageTabs.Members));
        Assert.True(AccountPageTabs.GetPosition(AccountPageTabs.Records) > AccountPageTabs.GetPosition(AccountPageTabs.Service));
    }

    private static async Task<JsonObject> ExportAsync(AccountsDeploymentSource source, AccountsDeploymentStep step)
    {
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        await source.ProcessDeploymentStepAsync(step, result);

        return Assert.Single(result.Steps);
    }

    private static ContentItem Account(string id, params AccountMember[] members)
    {
        var account = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = "Account",
            DisplayText = "Northwind Traders",
            Published = true,
            Latest = true,
        };

        account.Weld<AccountPart>();

        if (members.Length > 0)
        {
            account.Apply(new AccountMembersPart { Members = [.. members] });
        }

        return account;
    }

    private static ContentItem Contact(string id, string listId)
    {
        var contact = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = "Contact",
            DisplayText = id,
            Published = true,
            Latest = true,
        };

        contact.Apply(new ContainedPart { ListContentItemId = listId, ListContentType = "Account" });

        return contact;
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new ContentItemIndexProvider(), new TestContainedPartIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

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

        await schemaBuilder.CreateMapIndexTableAsync<ContainedPartIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ListContentItemId", column => column.WithLength(26))
            .Column<string>("ListContentType", column => column.WithLength(255))
            .Column<int>("Order")
            .Column<bool>("Published")
            .Column<bool>("Latest")
            .Column<string>("DisplayText", column => column.Nullable().WithLength(255)));

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    // Orchard Core's own provider needs its services; this one writes the same rows for the items the test saves.
    private sealed class TestContainedPartIndexProvider : IndexProvider<ContentItem>
    {
        public override void Describe(DescribeContext<ContentItem> context)
            => context.For<ContainedPartIndex>()
                .When(contentItem => contentItem.Has<ContainedPart>())
                .Map(contentItem =>
                {
                    contentItem.TryGet<ContainedPart>(out var part);

                    return new ContainedPartIndex
                    {
                        ContentItemId = contentItem.ContentItemId,
                        ListContentItemId = part.ListContentItemId,
                        ListContentType = part.ListContentType,
                        Order = part.Order,
                        Published = contentItem.Published,
                        Latest = contentItem.Latest,
                        DisplayText = contentItem.DisplayText,
                    };
                });
    }
}
