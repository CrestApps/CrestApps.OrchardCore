using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.WorkOrders.Core.Indexes;

/// <summary>
/// Indexes service agreements by number, status and customer.
/// </summary>
public sealed class ServiceAgreementIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the customer.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the company.
    /// </summary>
    public string CompanyId { get; set; }
}

/// <summary>
/// Maps service agreements to their index.
/// </summary>
public sealed class ServiceAgreementIndexProvider : IndexProvider<ServiceAgreement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementIndexProvider"/> class.
    /// </summary>
    public ServiceAgreementIndexProvider()
    {
        CollectionName = WorkOrdersConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ServiceAgreement> context)
    {
        context.For<ServiceAgreementIndex>()
            .Map(item => new ServiceAgreementIndex
            {
                ItemId = item.ItemId,
                Number = item.Number,
                Name = item.Name,
                Status = item.Status.ToString(),
                CustomerId = item.CustomerId,
                CompanyId = item.CompanyId,
            });
    }
}

/// <summary>
/// Creates the service agreement table.
/// </summary>
public static class ServiceAgreementsIndexSchema
{
    /// <summary>
    /// Creates the table and its indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = WorkOrdersConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<ServiceAgreementIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Number", column => column.Nullable().WithLength(32))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("CustomerId", column => column.Nullable().WithLength(64))
            .Column<string>("CompanyId", column => column.Nullable().WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ServiceAgreementIndex>(table =>
        {
            table.CreateIndex("IDX_ServiceAgreementIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ServiceAgreementIndex_Status", "Status", "Number");
            table.CreateIndex("IDX_ServiceAgreementIndex_Customer", "CustomerId", "CompanyId");
        }, collection: collection);
    }
}
