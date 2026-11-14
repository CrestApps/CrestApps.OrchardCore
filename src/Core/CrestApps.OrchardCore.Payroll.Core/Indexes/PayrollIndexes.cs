using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Payroll.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Payroll.Core.Indexes;

/// <summary>
/// Indexes employers.
/// </summary>
public sealed class EmployerIndex : CatalogItemIndex
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
/// Maps employers to <see cref="EmployerIndex"/> rows.
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
            .For<EmployerIndex>()
            .Map(item => new EmployerIndex
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
        => [new EmployerIndexProvider()];

    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = PayrollConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<EmployerIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("Currency", column => column.Nullable().WithLength(3)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<EmployerIndex>(table => table
            .CreateIndex("IDX_EmployerIndex_ItemId", "ItemId", "DocumentId"),
            collection: collection);
    }
}
