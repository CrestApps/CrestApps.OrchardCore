using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Subscriptions.Reports.DataSources;

/// <summary>
/// One row per line a subscription bills each cycle: its description, quantity and unit price. Each row carries the
/// subscription's status, currency, owner and start date, so a report can group the lines by them without joining
/// the subscriptions.
/// </summary>
public sealed class SubscriptionLinesReportDataSet : IndexedReportDataSet<Subscription, SubscriptionRecordIndex, ReportChildRow<Subscription, SubscriptionLine>>, ISubscriptionReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the subscription a line belongs to.
    /// </summary>
    public const string SubscriptionIdField = "SubscriptionId";

    /// <summary>
    /// The name of the field that holds when the subscription started, the data set's main date.
    /// </summary>
    public const string SubscriptionCreatedUtcField = "SubscriptionCreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionLinesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session subscriptions are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionLinesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<SubscriptionLinesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.SubscriptionSource.SubscriptionLines,
                stringLocalizer["Subscription lines"],
                stringLocalizer["One row per line a subscription bills each cycle: its description, quantity and price in the subscription's currency."]),
            session,
            authorizationService,
            SubscriptionConstants.SubscriptionCollectionName,
            SubscriptionPermissions.ManageSubscriptions)
    {
        var S = stringLocalizer;
        var subscription = S["Subscription"].Value;
        var line = S["Line"].Value;
        var inCurrency = S["In the subscription's currency."].Value;

        AddField(SubscriptionIdField, S["Subscription ID"], DataFieldType.Text, row => row.Parent.ItemId, subscription)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.SubscriptionSource.Name, CommerceReportDataSets.SubscriptionSource.Subscriptions));
        AddField("SubscriptionTitle", S["Subscription"], DataFieldType.Text, row => row.Parent.Title, subscription);
        AddField("SubscriptionStatus", S["Subscription status"], DataFieldType.Text, row => row.Parent.Status, subscription);
        AddField("Currency", S["Currency"], DataFieldType.Text, row => row.Parent.Currency, subscription);
        AddField(SubscriptionsReportDataSet.OwnerIdField, S["Owner ID"], DataFieldType.Text, row => row.Parent.OwnerId, subscription).Identifier(ReportDataReferences.User());
        AddField(SubscriptionCreatedUtcField, S["Subscription started"], DataFieldType.DateTime, row => row.Parent.CreatedUtc, subscription);

        AddField("LineId", S["Line ID"], DataFieldType.Text, row => row.Child.ItemId, line).Identifier();
        AddField("Position", S["Line number"], DataFieldType.Integer, row => row.Position, line);
        AddField(nameof(SubscriptionLine.Description), S["Description"], DataFieldType.Text, row => row.Child.Description, line);
        AddField(nameof(SubscriptionLine.Quantity), S["Quantity"], DataFieldType.Integer, row => row.Child.Quantity, line);
        AddField(nameof(SubscriptionLine.UnitPrice), S["Unit price"], DataFieldType.Decimal, row => row.Child.UnitPrice, line).Description = inCurrency;
        AddField("LineTotal", S["Line total"], DataFieldType.Decimal, row => row.Child.UnitPrice * row.Child.Quantity, line).Description = S["The unit price times the quantity, in the subscription's currency."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<SubscriptionRecordIndex, DateTime>> Column)? DateColumn
        => (SubscriptionCreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<SubscriptionRecordIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<SubscriptionRecordIndex, string>>>(StringComparer.Ordinal)
        {
            [SubscriptionIdField] = index => index.ItemId,
            [SubscriptionsReportDataSet.OwnerIdField] = index => index.OwnerId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<SubscriptionRecordIndex> columns)
    {
        columns
            .Enum("SubscriptionStatus", index => index.Status)
            .Text("Currency", nameof(SubscriptionRecordIndex.Currency));
    }

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<Subscription, SubscriptionLine>> GetRows(Subscription document)
    {
        return ReportChildRow<Subscription, SubscriptionLine>.For(document, document.Lines);
    }
}
