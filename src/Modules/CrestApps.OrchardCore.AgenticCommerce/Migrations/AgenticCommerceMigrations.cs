using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;
using OrchardCore.Data.Migration;
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

        return 1;
    }
}
