using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.TenantHierarchy.Migrations;

/// <summary>
/// Creates the index table of the user links of a child tenant.
/// </summary>
internal sealed class ChildTenantMigrations : DataMigration
{
    private const string Collection = TenantHierarchyConstants.CollectionName;

    /// <summary>
    /// Creates the index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<UserLinkIndex>(table => table
            .Column<string>(nameof(UserLinkIndex.ChildUserId), column => column.WithLength(26))
            .Column<string>(nameof(UserLinkIndex.ParentTenantId), column => column.WithLength(26))
            .Column<string>(nameof(UserLinkIndex.ParentUserId), column => column.WithLength(26))
            .Column<bool>(nameof(UserLinkIndex.IsCurrent)),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<UserLinkIndex>(table => table
            .CreateIndex("IDX_UserLinkIndex_ChildUserId", "DocumentId", nameof(UserLinkIndex.ChildUserId)),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<UserLinkIndex>(table => table
            .CreateIndex("IDX_UserLinkIndex_ParentUser", "DocumentId", nameof(UserLinkIndex.ParentTenantId), nameof(UserLinkIndex.ParentUserId), nameof(UserLinkIndex.IsCurrent)),
            collection: Collection);

        return 1;
    }
}
