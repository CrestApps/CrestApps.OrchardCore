using YesSql.Sql;

namespace CrestApps.OrchardCore.Payroll.Core.Indexes;

/// <summary>
/// Creates Pay Setup's index tables, for the feature's migration and for tests.
/// </summary>
public static class PaySetupIndexSchema
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = PayrollConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<PayrollPayScheduleIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayrollPayScheduleIndex>(table => table
            .CreateIndex("IDX_PayrollPayScheduleIndex_ItemId", "ItemId", "DocumentId"),
            collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<PayrollPayCodeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Code", column => column.Nullable().WithLength(50))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Class", column => column.Nullable().WithLength(50))
            .Column<string>("Kind", column => column.Nullable().WithLength(50))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayrollPayCodeIndex>(table => table
            .CreateIndex("IDX_PayrollPayCodeIndex_ItemId", "ItemId", "DocumentId"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayrollPayCodeIndex>(table => table
            .CreateIndex("IDX_PayrollPayCodeIndex_Code", "Code", "Enabled", "DocumentId"),
            collection: collection);
    }
}
