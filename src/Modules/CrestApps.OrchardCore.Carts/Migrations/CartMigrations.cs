using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Carts.Migrations;

/// <summary>
/// Creates the index table that finds carts by owner and by expiry.
/// </summary>
internal sealed class CartMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CartIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))

            // A SHA-256 hash in base64 is 44 characters.
            .Column<string>("GuestTokenHash", column => column.WithLength(64))
            .Column<DateTime>("ExpiresUtc"),
            collection: CartConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CartIndex>(table => table
            .CreateIndex("IDX_CartIndex_ItemId", "ItemId"),
            collection: CartConstants.CollectionName
        );

        // Every page with a cart link finds the visitor's cart by one of these, so neither may be a scan.
        await SchemaBuilder.AlterIndexTableAsync<CartIndex>(table => table
            .CreateIndex("IDX_CartIndex_UserId", "UserId"),
            collection: CartConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CartIndex>(table => table
            .CreateIndex("IDX_CartIndex_GuestTokenHash", "GuestTokenHash"),
            collection: CartConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CartIndex>(table => table
            .CreateIndex("IDX_CartIndex_ExpiresUtc", "ExpiresUtc"),
            collection: CartConstants.CollectionName
        );

        return 1;
    }
}
