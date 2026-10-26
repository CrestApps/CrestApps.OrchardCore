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
/// One row per installment plan: what it pays for, its customer, its total, what was received and what is still owed,
/// how its payments fall due and are collected, and where it stands. Money is in the plan's own currency, which the
/// <c>Currency</c> field names. The saved card, the note and the history messages are never exposed.
/// </summary>
public sealed class InstallmentPlansReportDataSet : IndexedReportDataSet<InstallmentPlan, InstallmentPlanIndex>, ISubscriptionReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the plan was created, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// The name of the field that holds the user the plan belongs to.
    /// </summary>
    public const string OwnerIdField = "OwnerId";

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlansReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session installment plans are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InstallmentPlansReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<InstallmentPlansReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.SubscriptionSource.InstallmentPlans,
                stringLocalizer["Installment plans"],
                stringLocalizer["One row per installment plan: its customer, its total, what was received and what is still owed in its currency, its schedule and its status."]),
            session,
            authorizationService,
            SubscriptionConstants.InstallmentPlanCollectionName,
            SubscriptionPermissions.ManageInstallmentPlans)
    {
        var S = stringLocalizer;
        var plan = S["Plan"].Value;
        var customer = S["Customer"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var inCurrency = S["In the plan's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Plan ID"], DataFieldType.Text, record => record.ItemId, plan).Identifier();
        AddField(nameof(InstallmentPlan.Title), S["Title"], DataFieldType.Text, record => record.Title, plan);
        AddField(nameof(InstallmentPlan.Status), S["Status"], DataFieldType.Text, record => record.Status, plan);
        AddField(nameof(InstallmentPlan.ReferenceType), S["Record type"], DataFieldType.Text, record => record.ReferenceType, plan).Description = S["The kind of record the plan pays for, as the module that owns it names it."].Value;
        AddField(nameof(InstallmentPlan.ReferenceId), S["Record ID"], DataFieldType.Text, record => record.ReferenceId, plan);
        AddField(nameof(InstallmentPlan.SourceTransactionId), S["Replaced transaction ID"], DataFieldType.Text, record => record.SourceTransactionId, plan)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.TransactionSource.Name, CommerceReportDataSets.TransactionSource.Transactions))
            .Description = S["The outstanding transaction the customer chose to pay in installments at checkout."].Value;
        AddField(nameof(InstallmentPlan.InstallmentCount), S["Scheduled payments"], DataFieldType.Integer, record => record.InstallmentCount, plan).Description = S["The number of payments after the down payment."].Value;
        AddField(nameof(InstallmentPlan.Frequency), S["Frequency"], DataFieldType.Text, record => record.Frequency, plan);
        AddField(nameof(InstallmentPlan.CollectionMethod), S["Collected by"], DataFieldType.Text, record => record.CollectionMethod, plan);
        AddField(nameof(InstallmentPlan.CreatedById), S["Created by user ID"], DataFieldType.Text, record => record.CreatedById, plan).Identifier(ReportDataReferences.User());
        AddField(nameof(InstallmentPlan.CreatedByName), S["Created by"], DataFieldType.Text, record => record.CreatedByName, plan);

        AddField(OwnerIdField, S["Customer user ID"], DataFieldType.Text, record => record.OwnerId, customer).Identifier(ReportDataReferences.User());
        AddField(nameof(InstallmentPlan.CustomerName), S["Customer name"], DataFieldType.Text, record => record.CustomerName, customer);
        AddField(nameof(InstallmentPlan.CustomerEmail), S["Customer email"], DataFieldType.Text, record => record.CustomerEmail, customer);

        AddField(nameof(InstallmentPlan.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, money);
        AddField(nameof(InstallmentPlan.TotalAmount), S["Total"], DataFieldType.Decimal, record => record.TotalAmount, money).Description = S["What the customer pays over the life of the plan, the down payment included, in the plan's currency."].Value;
        AddField(nameof(InstallmentPlan.DownPaymentAmount), S["Down payment"], DataFieldType.Decimal, record => record.DownPaymentAmount, money).Description = inCurrency;
        AddField(nameof(InstallmentPlan.AmountPaid), S["Received"], DataFieldType.Decimal, record => record.AmountPaid, money).Description = inCurrency;
        AddField(nameof(InstallmentPlan.AmountOutstanding), S["Outstanding"], DataFieldType.Decimal, record => record.AmountOutstanding, money).Description = S["What is still to be received, payments canceled with the plan left out, in the plan's currency."].Value;

        AddField(CreatedUtcField, S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(InstallmentPlan.FirstDueUtc), S["First due"], DataFieldType.DateTime, record => record.FirstDueUtc, dates);
        AddField(nameof(InstallmentPlanIndex.NextDueUtc), S["Next due"], DataFieldType.DateTime, record => record.NextPayment?.DueUtc, dates);
        AddField(nameof(InstallmentPlan.ActivatedUtc), S["Started"], DataFieldType.DateTime, record => record.ActivatedUtc, dates);
        AddField(nameof(InstallmentPlan.CompletedUtc), S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, dates);
        AddField(nameof(InstallmentPlan.CanceledUtc), S["Canceled"], DataFieldType.DateTime, record => record.CanceledUtc, dates);
        AddField(nameof(InstallmentPlan.UpdatedUtc), S["Last changed"], DataFieldType.DateTime, record => record.UpdatedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<InstallmentPlanIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<InstallmentPlanIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<InstallmentPlanIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [OwnerIdField] = index => index.OwnerId,
            [nameof(InstallmentPlan.SourceTransactionId)] = index => index.SourceTransactionId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<InstallmentPlanIndex> columns)
    {
        columns
            .Enum(nameof(InstallmentPlan.Status), index => index.Status)
            .Text(nameof(InstallmentPlan.Currency), nameof(InstallmentPlanIndex.Currency));
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index cuts these texts to 255 characters, so a longer value is not the field's value.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove(nameof(InstallmentPlan.Title));
        columns.Remove(nameof(InstallmentPlan.CustomerName));
        columns.Remove(nameof(InstallmentPlan.CustomerEmail));
        columns.Remove(nameof(InstallmentPlan.ReferenceType));
        columns.Remove(nameof(InstallmentPlan.ReferenceId));

        return columns;
    }
}
