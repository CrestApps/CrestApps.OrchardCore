using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AgenticCommerce.Migrations;

internal sealed class AgenticCommerceMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AgentPlatformIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<bool>("Enabled")
            .Column<string>("ApiKeyHash", column => column.WithLength(64))
            .Column<string>("OAuthClientId", column => column.WithLength(255))
            .Column<string>("ProfileUrl", column => column.WithLength(1000)),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentPlatformIndex>(table => table
            .CreateIndex("IDX_AgentPlatformIndex_ItemId", "ItemId", "DocumentId"),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentPlatformIndex>(table => table
            .CreateIndex("IDX_AgentPlatformIndex_ApiKeyHash", "ApiKeyHash", "DocumentId"),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentPlatformIndex>(table => table
            .CreateIndex("IDX_AgentPlatformIndex_OAuthClientId", "OAuthClientId", "DocumentId"),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.CreateMapIndexTableAsync<AgentCheckoutIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("PlatformId", column => column.WithLength(26))
            .Column<string>("Protocol", column => column.WithLength(20))
            .Column<bool>("IsTerminal")
            .Column<string>("CheckoutSessionId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<DateTime>("ExpiresUtc"),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentCheckoutIndex>(table => table
            .CreateIndex("IDX_AgentCheckoutIndex_ItemId", "ItemId", "DocumentId"),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentCheckoutIndex>(table => table
            .CreateIndex("IDX_AgentCheckoutIndex_Platform", "PlatformId", "IsTerminal", "DocumentId"),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentCheckoutIndex>(table => table
            .CreateIndex("IDX_AgentCheckoutIndex_Expiry", "IsTerminal", "ExpiresUtc", "DocumentId"),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentCheckoutIndex>(table => table
            .CreateIndex("IDX_AgentCheckoutIndex_Session", "CheckoutSessionId", "DocumentId"),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentCheckoutIndex>(table => table
            .CreateIndex("IDX_AgentCheckoutIndex_Order", "OrderId", "DocumentId"),
            collection: AgenticCommerceStorage.CollectionName);

        await AddProfileHostAndOriginAsync();

        return 2;
    }

    /// <summary>
    /// Adds the normalized host of a platform's profile to the platform index, so a profile saved with capitals or in
    /// Unicode is found by its host, and the key of an unregistered platform's origin to the checkout index, so the share
    /// of one origin is counted by 16 bytes of its hash rather than the 3 at the start of the platform id. The records
    /// saved before this step are indexed again once the tenant has started, outside activation, a page at a time.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await AddProfileHostAndOriginAsync();

        ShellScope.AddDeferredTask(scope => ReindexAsync(
            scope.ServiceProvider.GetRequiredService<ISession>(),
            scope.ServiceProvider.GetRequiredService<ILogger<AgenticCommerceMigrations>>()));

        return 2;
    }

    /// <summary>
    /// Maps the platforms and the checkouts again, a page at a time, so the columns of version 2 are filled in. A record
    /// that can't be mapped again is logged and skipped, and keeps its old index row until it is next saved.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="logger">The logger the skipped records are logged to.</param>
    internal static async Task ReindexAsync(ISession session, ILogger logger)
    {
        await CatalogIndexBackfill.ReindexAsync<AgentPlatform, AgentPlatformIndex>(session, AgenticCommerceStorage.CollectionName, logger: logger);
        await CatalogIndexBackfill.ReindexAsync<AgentCheckout, AgentCheckoutIndex>(session, AgenticCommerceStorage.CollectionName, logger: logger);
    }

    private async Task AddProfileHostAndOriginAsync()
    {
        await SchemaBuilder.AlterIndexTableAsync<AgentPlatformIndex>(table => table
            .AddColumn<string>("ProfileHost", column => column.Nullable().WithLength(255)),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentPlatformIndex>(table => table
            .CreateIndex("IDX_AgentPlatformIndex_ProfileHost", "ProfileHost", "DocumentId"),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentCheckoutIndex>(table => table
            .AddColumn<string>("PlatformOrigin", column => column.Nullable().WithLength(AgentPlatformAddress.OriginKeyLength)),
            collection: AgenticCommerceStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentCheckoutIndex>(table => table
            .CreateIndex("IDX_AgentCheckoutIndex_Origin", "PlatformOrigin", "IsTerminal", "DocumentId"),
            collection: AgenticCommerceStorage.CollectionName);
    }
}
