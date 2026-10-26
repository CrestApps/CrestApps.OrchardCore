using System.Linq.Expressions;
using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Modules;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Downloads.Reports.DataSources;

/// <summary>
/// One row per file a buyer may download from one order: the order and product it came with, how many times it was
/// downloaded and may still be, and whether its link still works. Where the file is kept, its id in the private store
/// and the buyer's link are never exposed.
/// </summary>
public sealed class DownloadEntitlementsReportDataSet : IndexedReportDataSet<DownloadEntitlement, DownloadEntitlementIndex>, IDownloadReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the entitlement was given, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadEntitlementsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session entitlements are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="clock">The clock that tells whether a link has expired.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DownloadEntitlementsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IClock clock,
        IStringLocalizer<DownloadEntitlementsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.DownloadSource.Entitlements,
                stringLocalizer["Download entitlements"],
                stringLocalizer["One row per file a buyer may download from one order: the order, product and file, the downloads made and left, and whether the link still works."]),
            session,
            authorizationService,
            DownloadsConstants.CollectionName,
            DownloadsPermissions.ManageDownloads)
    {
        var S = stringLocalizer;
        var download = S["Download"].Value;
        var order = S["Order"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Entitlement ID"], DataFieldType.Text, record => record.ItemId, download).Identifier();
        AddField("Status", S["Status"], DataFieldType.Text, record => Status(DownloadService.GetProblem(record, clock.UtcNow)), download).Description = S["Where the link stands when the report runs: Active, Revoked, Expired or LimitReached."].Value;
        AddField(nameof(DownloadEntitlement.DownloadCount), S["Downloads"], DataFieldType.Integer, record => record.DownloadCount, download);
        AddField(nameof(DownloadEntitlement.DownloadLimit), S["Download limit"], DataFieldType.Integer, record => record.DownloadLimit, download).Description = S["Empty when there is no limit."].Value;
        AddField(nameof(DownloadEntitlement.Remaining), S["Downloads left"], DataFieldType.Integer, record => record.Remaining, download).Description = S["Empty when there is no limit."].Value;
        AddField(nameof(DownloadEntitlement.ExpiresUtc), S["Expires"], DataFieldType.DateTime, record => record.ExpiresUtc, download).Description = S["Empty when the link never expires."].Value;
        AddField("IsRevoked", S["Taken back"], DataFieldType.Boolean, record => record.RevokedUtc.HasValue, download);
        AddField(nameof(DownloadEntitlement.RevokedUtc), S["Taken back on"], DataFieldType.DateTime, record => record.RevokedUtc, download);
        AddField(nameof(DownloadEntitlement.LastDownloadedUtc), S["Last downloaded"], DataFieldType.DateTime, record => record.LastDownloadedUtc, download);

        AddField(nameof(DownloadEntitlement.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, order)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(DownloadEntitlement.OrderNumber), S["Order number"], DataFieldType.Text, record => record.OrderNumber, order);
        AddField(nameof(DownloadEntitlement.OrderLineId), S["Order line ID"], DataFieldType.Text, record => record.OrderLineId, order)
            .Identifier(new DataFieldReference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.OrderLines, nameof(OrderLine.LineId)));
        AddField(nameof(DownloadEntitlement.ProductId), S["Product ID"], DataFieldType.Text, record => record.ProductId, order)
            .Identifier(CommerceReportDataSets.ProductReference());
        AddField(nameof(DownloadEntitlement.ProductTitle), S["Product"], DataFieldType.Text, record => record.ProductTitle, order);
        AddField(nameof(DownloadEntitlement.FileName), S["File"], DataFieldType.Text, record => record.FileName, order);

        AddField(CreatedUtcField, S["Given"], DataFieldType.DateTime, record => record.CreatedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<DownloadEntitlementIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<DownloadEntitlementIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<DownloadEntitlementIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(DownloadEntitlement.OrderId)] = index => index.OrderId,
        };

    private static string Status(DownloadProblem problem)
    {
        return problem switch
        {
            DownloadProblem.None => "Active",
            DownloadProblem.Revoked => "Revoked",
            DownloadProblem.Expired => "Expired",
            DownloadProblem.LimitReached => "LimitReached",
            _ => problem.ToString(),
        };
    }
}
