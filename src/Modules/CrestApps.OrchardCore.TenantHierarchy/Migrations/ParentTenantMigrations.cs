using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.TenantHierarchy.Migrations;

/// <summary>
/// Creates the index tables of a parent tenant and adds the default access grant.
/// </summary>
internal sealed class ParentTenantMigrations : DataMigration
{
    private const string Collection = TenantHierarchyConstants.CollectionName;

    private readonly AccessGrantManager _accessGrantManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentTenantMigrations"/> class.
    /// </summary>
    /// <param name="accessGrantManager">The access grant manager.</param>
    public ParentTenantMigrations(AccessGrantManager accessGrantManager)
    {
        _accessGrantManager = accessGrantManager;
    }

    /// <summary>
    /// Creates the index tables.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ChildTenantEntryIndex>(table => table
            .Column<string>(nameof(ChildTenantEntryIndex.EntryId), column => column.WithLength(26))
            .Column<string>(nameof(ChildTenantEntryIndex.TenantId), column => column.WithLength(26))
            .Column<string>(nameof(ChildTenantEntryIndex.TenantName), column => column.WithLength(64))
            .Column<string>(nameof(ChildTenantEntryIndex.Slug), column => column.WithLength(64))
            .Column<string>(nameof(ChildTenantEntryIndex.DisplayName), column => column.WithLength(255))
            .Column<string>(nameof(ChildTenantEntryIndex.Status), column => column.WithLength(32))
            .Column<DateTime>(nameof(ChildTenantEntryIndex.CreatedUtc))
            .Column<DateTime?>(nameof(ChildTenantEntryIndex.RetainUntilUtc), column => column.Nullable()),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<ChildTenantEntryIndex>(table => table
            .CreateIndex("IDX_ChildTenantEntryIndex_EntryId", "DocumentId", nameof(ChildTenantEntryIndex.EntryId), nameof(ChildTenantEntryIndex.TenantId), nameof(ChildTenantEntryIndex.Slug)),
            collection: Collection);

        await SchemaBuilder.CreateMapIndexTableAsync<AccessGrantIndex>(table => table
            .Column<string>(nameof(AccessGrantIndex.GrantId), column => column.WithLength(26))
            .Column<string>(nameof(AccessGrantIndex.PrincipalType), column => column.WithLength(16))
            .Column<string>(nameof(AccessGrantIndex.PrincipalId), column => column.WithLength(255))
            .Column<string>(nameof(AccessGrantIndex.ChildEntryId), column => column.Nullable().WithLength(26)),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<AccessGrantIndex>(table => table
            .CreateIndex("IDX_AccessGrantIndex_Principal", "DocumentId", nameof(AccessGrantIndex.PrincipalType), nameof(AccessGrantIndex.PrincipalId), nameof(AccessGrantIndex.ChildEntryId)),
            collection: Collection);

        await SchemaBuilder.CreateMapIndexTableAsync<DelegatedAccessCodeIndex>(table => table
            .Column<string>(nameof(DelegatedAccessCodeIndex.CodeHash), column => column.WithLength(64))
            .Column<DateTime>(nameof(DelegatedAccessCodeIndex.ExpiresUtc)),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<DelegatedAccessCodeIndex>(table => table
            .CreateIndex("IDX_DelegatedAccessCodeIndex_CodeHash", "DocumentId", nameof(DelegatedAccessCodeIndex.CodeHash), nameof(DelegatedAccessCodeIndex.ExpiresUtc)),
            collection: Collection);

        await SchemaBuilder.CreateMapIndexTableAsync<DelegatedAccessSessionIndex>(table => table
            .Column<string>(nameof(DelegatedAccessSessionIndex.SessionHash), column => column.WithLength(64))
            .Column<string>(nameof(DelegatedAccessSessionIndex.ParentUserId), column => column.WithLength(26))
            .Column<string>(nameof(DelegatedAccessSessionIndex.ParentSessionId), column => column.Nullable().WithLength(64))
            .Column<string>(nameof(DelegatedAccessSessionIndex.ChildEntryId), column => column.WithLength(26))
            .Column<bool>(nameof(DelegatedAccessSessionIndex.IsOpen))
            .Column<DateTime>(nameof(DelegatedAccessSessionIndex.CreatedUtc)),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<DelegatedAccessSessionIndex>(table => table
            .CreateIndex("IDX_DelegatedAccessSessionIndex_SessionHash", "DocumentId", nameof(DelegatedAccessSessionIndex.SessionHash), nameof(DelegatedAccessSessionIndex.IsOpen)),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<DelegatedAccessSessionIndex>(table => table
            .CreateIndex("IDX_DelegatedAccessSessionIndex_Owner", "DocumentId", nameof(DelegatedAccessSessionIndex.ParentUserId), nameof(DelegatedAccessSessionIndex.ParentSessionId), nameof(DelegatedAccessSessionIndex.ChildEntryId), nameof(DelegatedAccessSessionIndex.IsOpen)),
            collection: Collection);

        await SchemaBuilder.CreateMapIndexTableAsync<HierarchyAuditEventIndex>(table => table
            .Column<string>(nameof(HierarchyAuditEventIndex.Name), column => column.WithLength(64))
            .Column<string>(nameof(HierarchyAuditEventIndex.ChildEntryId), column => column.Nullable().WithLength(26))
            .Column<string>(nameof(HierarchyAuditEventIndex.UserId), column => column.Nullable().WithLength(26))
            .Column<DateTime>(nameof(HierarchyAuditEventIndex.CreatedUtc)),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<HierarchyAuditEventIndex>(table => table
            .CreateIndex("IDX_HierarchyAuditEventIndex_Child", "DocumentId", nameof(HierarchyAuditEventIndex.ChildEntryId), nameof(HierarchyAuditEventIndex.CreatedUtc)),
            collection: Collection);

        await SchemaBuilder.CreateMapIndexTableAsync<TenantSwitcherPreferenceIndex>(table => table
            .Column<string>(nameof(TenantSwitcherPreferenceIndex.UserId), column => column.WithLength(26)),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<TenantSwitcherPreferenceIndex>(table => table
            .CreateIndex("IDX_TenantSwitcherPreferenceIndex_UserId", "DocumentId", nameof(TenantSwitcherPreferenceIndex.UserId)),
            collection: Collection);

        await _accessGrantManager.SeedDefaultAsync();

        return 1;
    }
}
