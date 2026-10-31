using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// Creates the scheduling index tables, for the module's migration and the tests.
/// </summary>
public static class SchedulingIndexSchema
{
    /// <summary>
    /// Creates the index tables in the <see cref="SchedulingConstants.CollectionName"/> collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = SchedulingConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<SchedulingResourceIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SchedulingResourceIndex>(table => table.CreateIndex("IDX_SchedulingResourceIndex_ItemId", "ItemId"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AppointmentTypeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Slug", column => column.Nullable().WithLength(100))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AppointmentTypeIndex>(table =>
        {
            table.CreateIndex("IDX_AppointmentTypeIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_AppointmentTypeIndex_Slug", "Slug");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AppointmentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AppointmentTypeId", column => column.WithLength(26))
            .Column<DateTime>("StartUtc")
            .Column<DateTime>("EndUtc")
            .Column<AppointmentStatus>("Status")
            .Column<DateTime>("HoldExpiresUtc", column => column.Nullable())
            .Column<string>("CustomerName", column => column.Nullable().WithLength(255))
            .Column<string>("CustomerEmail", column => column.Nullable().WithLength(255))
            .Column<string>("CustomerPhone", column => column.Nullable().WithLength(50))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AppointmentIndex>(table =>
        {
            table.CreateIndex("IDX_AppointmentIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_AppointmentIndex_Start", "StartUtc", "Status");
            table.CreateIndex("IDX_AppointmentIndex_Hold", "Status", "HoldExpiresUtc");
            table.CreateIndex("IDX_AppointmentIndex_Email", "CustomerEmail");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AppointmentResourceIndex>(table => table
            .Column<string>("AppointmentId", column => column.WithLength(26))
            .Column<string>("AppointmentTypeId", column => column.WithLength(26))
            .Column<string>("ResourceId", column => column.WithLength(26))
            .Column<DateTime>("StartUtc")
            .Column<DateTime>("EndUtc")
            .Column<AppointmentStatus>("Status"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AppointmentResourceIndex>(table => table.CreateIndex("IDX_AppointmentResourceIndex_Resource", "ResourceId", "StartUtc"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<WaitlistEntryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AppointmentTypeId", column => column.WithLength(26))
            .Column<DateTime>("FromUtc")
            .Column<DateTime>("ToUtc")
            .Column<WaitlistStatus>("Status")
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<WaitlistEntryIndex>(table => table.CreateIndex("IDX_WaitlistEntryIndex_Type", "AppointmentTypeId", "Status"), collection: collection);
    }
}
