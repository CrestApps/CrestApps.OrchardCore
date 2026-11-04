using System.Linq.Expressions;
using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Dispatch.Reports;

/// <summary>
/// One row per asset: its type, owner, state, place and warranty.
/// </summary>
public sealed class AssetsReportDataSet : IndexedReportDataSet<Asset, AssetIndex>, IFieldServiceReportDataSet
{
    /// <summary>
    /// The data set's name.
    /// </summary>
    public const string DataSetName = "FieldServiceAssets";

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssetsReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<AssetsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Assets"], stringLocalizer["One row per asset: type, owner, state, where it is, when it was installed and its warranty."]), session, authorizationService, AssetsConstants.CollectionName, AssetsPermissions.ViewAssets)
    {
        var S = stringLocalizer;
        var group = S["Asset"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("Number", S["Number"], DataFieldType.Text, record => record.Number, group);
        AddField("Name", S["Name"], DataFieldType.Text, record => record.Name, group);
        AddField("AssetTypeId", S["Type ID"], DataFieldType.Text, record => record.AssetTypeId, group);
        AddField("Status", S["State"], DataFieldType.Text, record => record.Status.ToString(), group);
        AddField("OwnerKind", S["Owned by"], DataFieldType.Text, record => record.OwnerKind.ToString(), group);
        AddField("OwnerId", S["Owner ID"], DataFieldType.Text, record => record.OwnerId, group);
        AddField("ParentAssetId", S["Part of"], DataFieldType.Text, record => record.ParentAssetId, group);
        AddField("SerialNumber", S["Serial number"], DataFieldType.Text, record => record.SerialNumber, group);
        AddField("Manufacturer", S["Manufacturer"], DataFieldType.Text, record => record.Manufacturer, group);
        AddField("Model", S["Model"], DataFieldType.Text, record => record.Model, group);
        AddField("City", S["City"], DataFieldType.Text, record => record.Location?.City, group);
        AddField("PostalCode", S["Postal code"], DataFieldType.Text, record => record.Location?.PostalCode, group);
        AddField("InstallDate", S["Installed"], DataFieldType.Date, record => record.InstallDate?.ToDateTime(TimeOnly.MinValue), group);
        AddField("WarrantyEnd", S["Warranty ends"], DataFieldType.Date, record => record.Warranty?.EndDate?.ToDateTime(TimeOnly.MinValue), group);
        AddField("Schedulable", S["Bookable"], DataFieldType.Boolean, record => record.Fleet?.Schedulable ?? false, group);
        AddField("CarriesStock", S["Carries stock"], DataFieldType.Boolean, record => record.Fleet?.CarriesStock ?? false, group);
        AddField("CreatedUtc", S["Added"], DataFieldType.DateTime, record => record.CreatedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<AssetIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AssetIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["AssetTypeId"] = index => index.AssetTypeId,
            ["Status"] = index => index.Status,
            ["OwnerKind"] = index => index.OwnerKind,
            ["OwnerId"] = index => index.OwnerId,
        };
}

/// <summary>
/// One row per meter reading.
/// </summary>
public sealed class MeterReadingsReportDataSet : IndexedReportDataSet<AssetMeterReading, AssetMeterReadingIndex>, IFieldServiceReportDataSet
{
    /// <summary>
    /// The data set's name.
    /// </summary>
    public const string DataSetName = "FieldServiceMeterReadings";

    /// <summary>
    /// Initializes a new instance of the <see cref="MeterReadingsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MeterReadingsReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<MeterReadingsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Meter readings"], stringLocalizer["One row per meter reading: the asset, the meter, the value, when, and where it came from."]), session, authorizationService, AssetsConstants.CollectionName, AssetsPermissions.ViewAssets)
    {
        var S = stringLocalizer;
        var group = S["Meter reading"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("AssetId", S["Asset ID"], DataFieldType.Text, record => record.AssetId, group);
        AddField("Meter", S["Meter"], DataFieldType.Text, record => record.Meter, group);
        AddField("Value", S["Value"], DataFieldType.Decimal, record => record.Value, group);
        AddField("Utc", S["Read"], DataFieldType.DateTime, record => record.Utc, group);
        AddField("Source", S["Source"], DataFieldType.Text, record => record.Source.ToString(), group);
        AddField("SourceId", S["Source ID"], DataFieldType.Text, record => record.SourceId, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<AssetMeterReadingIndex, DateTime>> Column)? DateColumn => ("Utc", index => index.Utc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<AssetMeterReadingIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AssetMeterReadingIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["AssetId"] = index => index.AssetId,
            ["Meter"] = index => index.Meter,
        };
}

/// <summary>
/// A row of an asset's service history: one asset on one work order.
/// </summary>
public sealed class ServiceHistoryRow
{
    /// <summary>
    /// Gets the asset.
    /// </summary>
    public string AssetId { get; init; }

    /// <summary>
    /// Gets the work order.
    /// </summary>
    public WorkOrder WorkOrder { get; init; }
}

/// <summary>
/// One row for each asset on each work order: the assets' service history.
/// </summary>
public sealed class ServiceHistoryReportDataSet : IndexedReportDataSet<WorkOrder, WorkOrderIndex, ServiceHistoryRow>, IFieldServiceReportDataSet
{
    /// <summary>
    /// The data set's name.
    /// </summary>
    public const string DataSetName = "FieldServiceServiceHistory";

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceHistoryReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ServiceHistoryReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<ServiceHistoryReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Service history"], stringLocalizer["One row for each asset on each work order: the work order's type, status, when it was made and completed, and the parts used."]), session, authorizationService, WorkOrdersConstants.CollectionName, WorkOrdersPermissions.ViewWorkOrders, AssetsPermissions.ViewAssets)
    {
        var S = stringLocalizer;
        var group = S["Service"].Value;

        AddField("AssetId", S["Asset ID"], DataFieldType.Text, row => row.AssetId, group);
        AddField("WorkOrderId", S["Work order ID"], DataFieldType.Text, row => row.WorkOrder.ItemId, group);
        AddField("WorkOrderNumber", S["Work order"], DataFieldType.Text, row => row.WorkOrder.Number, group);
        AddField("TypeId", S["Type ID"], DataFieldType.Text, row => row.WorkOrder.TypeId, group);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.WorkOrder.Status.ToString(), group);
        AddField("Summary", S["Summary"], DataFieldType.Text, row => row.WorkOrder.Summary, group);
        AddField("CreatedUtc", S["Made"], DataFieldType.DateTime, row => row.WorkOrder.CreatedUtc, group);
        AddField("CompletedUtc", S["Completed"], DataFieldType.DateTime, row => row.WorkOrder.CompletedUtc, group);
        AddField("PartsUsed", S["Parts used"], DataFieldType.Decimal, row => row.WorkOrder.Lines.Where(line => line.Kind == WorkOrderLineKind.Product).Sum(line => line.QuantityUsed), group);
        AddField("FollowUpOf", S["Follow-up of"], DataFieldType.Text, row => row.WorkOrder.ParentWorkOrderId, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<WorkOrderIndex, DateTime>> Column)? DateColumn => ("CreatedUtc", index => index.CreatedUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<WorkOrderIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<WorkOrderIndex, string>>>(StringComparer.Ordinal)
        {
            ["WorkOrderId"] = index => index.ItemId,
            ["TypeId"] = index => index.TypeId,
            ["Status"] = index => index.Status,
        };

    /// <inheritdoc />
    protected override IEnumerable<ServiceHistoryRow> GetRows(WorkOrder document)
        => document.AssetIds.Distinct(StringComparer.Ordinal).Select(assetId => new ServiceHistoryRow { AssetId = assetId, WorkOrder = document });
}
