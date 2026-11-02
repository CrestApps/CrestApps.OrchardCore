using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.WorkOrders.Core.Indexes;

/// <summary>
/// Indexes work orders.
/// </summary>
public sealed class WorkOrderIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is still open.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public string Priority { get; set; }

    /// <summary>
    /// Gets or sets the summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the customer.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the company customer.
    /// </summary>
    public string CompanyId { get; set; }

    /// <summary>
    /// Gets or sets the CRM contact.
    /// </summary>
    public string ContactId { get; set; }

    /// <summary>
    /// Gets or sets the territory.
    /// </summary>
    public string TerritoryId { get; set; }

    /// <summary>
    /// Gets or sets when the first response is due.
    /// </summary>
    public DateTime? RespondByUtc { get; set; }

    /// <summary>
    /// Gets or sets when it must be resolved.
    /// </summary>
    public DateTime? ResolveByUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets where it came from.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the record it came from.
    /// </summary>
    public string SourceId { get; set; }
}

/// <summary>
/// One row per asset a work order is for.
/// </summary>
public sealed class WorkOrderAssetIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public string WorkOrderId { get; set; }

    /// <summary>
    /// Gets or sets the asset.
    /// </summary>
    public string AssetId { get; set; }

    /// <summary>
    /// Gets or sets the work order's status.
    /// </summary>
    public string Status { get; set; }
}

/// <summary>
/// Indexes a kind of work order settings record by name. Each kind has its own table, so listing one kind never reads
/// another.
/// </summary>
public abstract class WorkOrderSettingIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// Indexes the work order types.
/// </summary>
public sealed class WorkOrderTypeIndex : WorkOrderSettingIndex
{
}

/// <summary>
/// Indexes the checklist templates.
/// </summary>
public sealed class ChecklistTemplateIndex : WorkOrderSettingIndex
{
}

/// <summary>
/// Indexes the service level policies.
/// </summary>
public sealed class ServiceLevelPolicyIndex : WorkOrderSettingIndex
{
}

/// <summary>
/// Indexes the sub-statuses.
/// </summary>
public sealed class WorkOrderSubStatusIndex : WorkOrderSettingIndex
{
}

/// <summary>
/// Maps work orders.
/// </summary>
public sealed class WorkOrderIndexProvider : IndexProvider<WorkOrder>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderIndexProvider"/> class.
    /// </summary>
    public WorkOrderIndexProvider()
    {
        CollectionName = WorkOrdersConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<WorkOrder> context)
    {
        context.For<WorkOrderIndex>()
            .Map(item => new WorkOrderIndex
            {
                ItemId = item.ItemId,
                Number = item.Number,
                TypeId = item.TypeId,
                Status = item.Status.ToString(),
                IsOpen = WorkOrderStatusRules.IsOpen(item.Status),
                Priority = item.Priority.ToString(),
                Summary = item.Summary,
                CustomerId = item.CustomerId,
                CompanyId = item.CompanyId,
                ContactId = item.ContactId,
                TerritoryId = item.TerritoryId,
                RespondByUtc = item.Sla?.RespondByUtc,
                ResolveByUtc = item.Sla?.ResolveByUtc,
                CreatedUtc = item.CreatedUtc,
                Source = item.Source.ToString(),
                SourceId = item.SourceId,
            });

        context.For<WorkOrderAssetIndex>()
            .Map(item => item.AssetIds.Distinct(StringComparer.Ordinal).Select(assetId => new WorkOrderAssetIndex { WorkOrderId = item.ItemId, AssetId = assetId, Status = item.Status.ToString() }));
    }
}

/// <summary>
/// Maps work order types.
/// </summary>
public sealed class WorkOrderTypeIndexProvider : IndexProvider<WorkOrderType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderTypeIndexProvider"/> class.
    /// </summary>
    public WorkOrderTypeIndexProvider()
    {
        CollectionName = WorkOrdersConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<WorkOrderType> context)
    {
        context.For<WorkOrderTypeIndex>()
            .Map(item => new WorkOrderTypeIndex { ItemId = item.ItemId, Name = item.Name });
    }
}

/// <summary>
/// Maps checklist templates.
/// </summary>
public sealed class ChecklistTemplateIndexProvider : IndexProvider<ChecklistTemplate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChecklistTemplateIndexProvider"/> class.
    /// </summary>
    public ChecklistTemplateIndexProvider()
    {
        CollectionName = WorkOrdersConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ChecklistTemplate> context)
    {
        context.For<ChecklistTemplateIndex>()
            .Map(item => new ChecklistTemplateIndex { ItemId = item.ItemId, Name = item.Name });
    }
}

/// <summary>
/// Maps service level policies.
/// </summary>
public sealed class ServiceLevelPolicyIndexProvider : IndexProvider<ServiceLevelPolicy>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelPolicyIndexProvider"/> class.
    /// </summary>
    public ServiceLevelPolicyIndexProvider()
    {
        CollectionName = WorkOrdersConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ServiceLevelPolicy> context)
    {
        context.For<ServiceLevelPolicyIndex>()
            .Map(item => new ServiceLevelPolicyIndex { ItemId = item.ItemId, Name = item.Name });
    }
}

/// <summary>
/// Maps sub-statuses.
/// </summary>
public sealed class WorkOrderSubStatusIndexProvider : IndexProvider<WorkOrderSubStatus>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderSubStatusIndexProvider"/> class.
    /// </summary>
    public WorkOrderSubStatusIndexProvider()
    {
        CollectionName = WorkOrdersConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<WorkOrderSubStatus> context)
    {
        context.For<WorkOrderSubStatusIndex>()
            .Map(item => new WorkOrderSubStatusIndex { ItemId = item.ItemId, Name = item.Name });
    }
}

/// <summary>
/// Creates the work order index tables.
/// </summary>
public static class WorkOrdersIndexSchema
{
    /// <summary>
    /// Creates the tables and their indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the tables exist.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = WorkOrdersConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<WorkOrderIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Number", column => column.Nullable().WithLength(32))
            .Column<string>("TypeId", column => column.Nullable().WithLength(26))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<bool>("IsOpen")
            .Column<string>("Priority", column => column.Nullable().WithLength(20))
            .Column<string>("Summary", column => column.Nullable().WithLength(255))
            .Column<string>("CustomerId", column => column.Nullable().WithLength(64))
            .Column<string>("CompanyId", column => column.Nullable().WithLength(64))
            .Column<string>("ContactId", column => column.Nullable().WithLength(64))
            .Column<string>("TerritoryId", column => column.Nullable().WithLength(26))
            .Column<DateTime?>("RespondByUtc", column => column.Nullable())
            .Column<DateTime?>("ResolveByUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc")
            .Column<string>("Source", column => column.Nullable().WithLength(20))
            .Column<string>("SourceId", column => column.Nullable().WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<WorkOrderIndex>(table =>
        {
            table.CreateIndex("IDX_WorkOrderIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_WorkOrderIndex_Number", "Number");
            table.CreateIndex("IDX_WorkOrderIndex_Open", "IsOpen", "Status", "Priority");
            table.CreateIndex("IDX_WorkOrderIndex_Customer", "CustomerId", "CompanyId", "ContactId");
            table.CreateIndex("IDX_WorkOrderIndex_Sla", "IsOpen", "RespondByUtc", "ResolveByUtc");
            table.CreateIndex("IDX_WorkOrderIndex_Source", "Source", "SourceId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<WorkOrderAssetIndex>(table => table
            .Column<string>("WorkOrderId", column => column.WithLength(26))
            .Column<string>("AssetId", column => column.WithLength(26))
            .Column<string>("Status", column => column.Nullable().WithLength(20)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<WorkOrderAssetIndex>(table => table.CreateIndex("IDX_WorkOrderAssetIndex_Asset", "AssetId", "Status"), collection: collection);

        await CreateSettingTableAsync<WorkOrderTypeIndex>(schemaBuilder, collection);
        await CreateSettingTableAsync<ChecklistTemplateIndex>(schemaBuilder, collection);
        await CreateSettingTableAsync<ServiceLevelPolicyIndex>(schemaBuilder, collection);
        await CreateSettingTableAsync<WorkOrderSubStatusIndex>(schemaBuilder, collection);
    }

    private static async Task CreateSettingTableAsync<TIndex>(ISchemaBuilder schemaBuilder, string collection)
        where TIndex : WorkOrderSettingIndex
    {
        await schemaBuilder.CreateMapIndexTableAsync<TIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TIndex>(table => table.CreateIndex($"IDX_{typeof(TIndex).Name}_ItemId", "ItemId"), collection: collection);
    }
}
