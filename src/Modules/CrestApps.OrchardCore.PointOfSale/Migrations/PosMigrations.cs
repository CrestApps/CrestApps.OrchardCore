using CrestApps.OrchardCore.PointOfSale.Indexes;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.PointOfSale.Migrations;

/// <summary>
/// Creates the indexes of registers and their shifts.
/// </summary>
internal sealed class PosMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<RegisterIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("LocationId", column => column.WithLength(26))
            .Column<bool>("IsEnabled"),
            collection: PosConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<RegisterShiftIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("RegisterId", column => column.WithLength(26))
            .Column<string>("LocationId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("OpenedUtc"),
            collection: PosConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<RegisterIndex>(table => table
            .CreateIndex("IDX_RegisterIndex_ItemId", "ItemId"),
            collection: PosConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<RegisterShiftIndex>(table =>
        {
            table.CreateIndex("IDX_RegisterShiftIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_RegisterShiftIndex_Register", "RegisterId", "Status");
        }, collection: PosConstants.CollectionName);

        await CreateReportIndexAsync();
        await AddUserColumnsAsync();
        await CreateDepartmentIndexAsync();

        return 4;
    }

    /// <summary>
    /// Adds the index of the departments registers ring amounts up in.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await CreateDepartmentIndexAsync();

        return 4;
    }

    private async Task CreateDepartmentIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PosDepartmentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Number", column => column.Nullable().WithLength(50))
            .Column<int>("Position")
            .Column<bool>("IsEnabled"),
            collection: PosConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<PosDepartmentIndex>(table =>
        {
            table.CreateIndex("IDX_PosDepartmentIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_PosDepartmentIndex_Position", "Position", "Name");
        }, collection: PosConstants.CollectionName);
    }

    /// <summary>
    /// Adds the index reports read shifts through, newest first by <c>OpenedUtc</c> a page at a time, so each page and
    /// each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the users who opened and closed a shift to the shift index, so a privacy request finds a person's shifts.
    /// Shifts saved before this step are indexed again once the tenant has started, outside activation.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await AddUserColumnsAsync();

        ShellScope.AddDeferredTask(scope => CatalogIndexBackfill.ReindexAsync<RegisterShift, RegisterShiftIndex>(
            scope.ServiceProvider.GetRequiredService<ISession>(),
            PosConstants.CollectionName));

        return 3;
    }

    private async Task AddUserColumnsAsync()
    {
        await SchemaBuilder.AlterIndexTableAsync<RegisterShiftIndex>(table =>
        {
            table.AddColumn<string>("OpenedByUserId", column => column.Nullable().WithLength(26));
            table.AddColumn<string>("ClosedByUserId", column => column.Nullable().WithLength(26));
        }, collection: PosConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<RegisterShiftIndex>(table =>
        {
            table.CreateIndex("IDX_RegisterShiftIndex_OpenedBy", "OpenedByUserId");
            table.CreateIndex("IDX_RegisterShiftIndex_ClosedBy", "ClosedByUserId");
        }, collection: PosConstants.CollectionName);
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<RegisterShiftIndex>(
            SchemaBuilder,
            "IDX_RegisterShiftIndex_Report",
            PosConstants.CollectionName,
            "OpenedUtc", "Id");
    }
}
