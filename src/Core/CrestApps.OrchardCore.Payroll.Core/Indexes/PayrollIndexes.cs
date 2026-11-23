using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Payroll.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Payroll.Core.Indexes;

/// <summary>
/// Indexes employers.
/// </summary>
public sealed class PayrollEmployerIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }
}

/// <summary>
/// Indexes pay profiles: by staff member, employer, schedule and the keyed hash of the tax id.
/// </summary>
public sealed class PayrollPayProfileIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the staff member.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets the employer.
    /// </summary>
    public string EmployerId { get; set; }

    /// <summary>
    /// Gets or sets the pay schedule.
    /// </summary>
    public string PayScheduleId { get; set; }

    /// <summary>
    /// Gets or sets the worker type.
    /// </summary>
    public string WorkerType { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the keyed hash of the tax id.
    /// </summary>
    public string TaxIdHash { get; set; }
}

/// <summary>
/// Indexes pay items: by profile, staff member, employer and pay code.
/// </summary>
public sealed class PayrollPayItemIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the pay profile.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the staff member.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets the employer.
    /// </summary>
    public string EmployerId { get; set; }

    /// <summary>
    /// Gets or sets the pay code.
    /// </summary>
    public string PayCodeId { get; set; }
}

/// <summary>
/// Maps pay profiles to <see cref="PayrollPayProfileIndex"/> rows.
/// </summary>
public sealed class PayProfileIndexProvider : IndexProvider<EmployeePayProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayProfileIndexProvider"/> class.
    /// </summary>
    public PayProfileIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<EmployeePayProfile> context)
    {
        context
            .For<PayrollPayProfileIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new PayrollPayProfileIndex
            {
                ItemId = item.ItemId,
                StaffMemberId = item.StaffMemberId,
                EmployerId = item.EmployerId,
                PayScheduleId = item.PayScheduleId,
                WorkerType = item.WorkerType.ToString(),
                Status = item.Status.ToString(),
                TaxIdHash = item.TaxIdHash,
            });
    }
}

/// <summary>
/// Maps pay items to <see cref="PayrollPayItemIndex"/> rows.
/// </summary>
public sealed class PayItemIndexProvider : IndexProvider<EmployeePayItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayItemIndexProvider"/> class.
    /// </summary>
    public PayItemIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<EmployeePayItem> context)
    {
        context
            .For<PayrollPayItemIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new PayrollPayItemIndex
            {
                ItemId = item.ItemId,
                ProfileId = item.ProfileId,
                StaffMemberId = item.StaffMemberId,
                EmployerId = item.EmployerId,
                PayCodeId = item.PayCodeId,
            });
    }
}

/// <summary>
/// Maps employers to <see cref="PayrollEmployerIndex"/> rows.
/// </summary>
public sealed class EmployerIndexProvider : IndexProvider<Employer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmployerIndexProvider"/> class.
    /// </summary>
    public EmployerIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Employer> context)
    {
        context
            .For<PayrollEmployerIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new PayrollEmployerIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Status = item.Status.ToString(),
                Currency = item.Currency,
            });
    }
}

/// <summary>
/// The index providers and tables of the Payroll feature.
/// </summary>
public static class PayrollIndexSchema
{
    /// <summary>
    /// Returns a new instance of each provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] Providers()
        => [new EmployerIndexProvider(), new PayProfileIndexProvider(), new PayItemIndexProvider(), new PayStatementIndexProvider(), new PayStatementLineIndexProvider(), new CrestApps.OrchardCore.Payroll.Core.Services.PayRunIndexProvider(), new CrestApps.OrchardCore.Payroll.Core.Services.PayRunInputIndexProvider()];

    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await CreateEmployerTableAsync(schemaBuilder);
        await CreateProfileTablesAsync(schemaBuilder);
        await PayStatementIndexSchema.CreateAsync(schemaBuilder);
        await CrestApps.OrchardCore.Payroll.Core.Services.PayRunIndexSchema.CreateAsync(schemaBuilder);
    }

    /// <summary>
    /// Creates the table of employers.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateEmployerTableAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = PayrollConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<PayrollEmployerIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("Currency", column => column.Nullable().WithLength(3)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayrollEmployerIndex>(table => table
            .CreateIndex("IDX_PayrollEmployerIndex_ItemId", "ItemId", "DocumentId"),
            collection: collection);
    }

    /// <summary>
    /// Creates the tables of pay profiles and pay items, added after the first version.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateProfileTablesAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = PayrollConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<PayrollPayProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("StaffMemberId", column => column.Nullable().WithLength(26))
            .Column<string>("EmployerId", column => column.Nullable().WithLength(26))
            .Column<string>("PayScheduleId", column => column.Nullable().WithLength(26))
            .Column<string>("WorkerType", column => column.Nullable().WithLength(20))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("TaxIdHash", column => column.Nullable().WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayrollPayProfileIndex>(table =>
        {
            table.CreateIndex("IDX_PayrollPayProfileIndex_ItemId", "ItemId", "DocumentId");
            table.CreateIndex("IDX_PayrollPayProfileIndex_Staff", "StaffMemberId", "EmployerId", "DocumentId");
            table.CreateIndex("IDX_PayrollPayProfileIndex_Employer", "EmployerId", "Status", "DocumentId");
            table.CreateIndex("IDX_PayrollPayProfileIndex_TaxId", "TaxIdHash", "DocumentId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<PayrollPayItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProfileId", column => column.Nullable().WithLength(26))
            .Column<string>("StaffMemberId", column => column.Nullable().WithLength(26))
            .Column<string>("EmployerId", column => column.Nullable().WithLength(26))
            .Column<string>("PayCodeId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayrollPayItemIndex>(table =>
        {
            table.CreateIndex("IDX_PayrollPayItemIndex_ItemId", "ItemId", "DocumentId");
            table.CreateIndex("IDX_PayrollPayItemIndex_Profile", "ProfileId", "DocumentId");
            table.CreateIndex("IDX_PayrollPayItemIndex_PayCode", "PayCodeId", "DocumentId");
        }, collection: collection);
    }
}
