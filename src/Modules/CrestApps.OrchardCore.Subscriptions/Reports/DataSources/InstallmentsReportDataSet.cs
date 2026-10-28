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
/// One row per payment of an installment plan, the down payment included: when it falls due, how much it is, where it
/// stands, and the ledger transaction that records it. Each row carries the plan's title, status, currency and
/// customer, so a report can group the payments by them without joining the plans. What the payment provider said
/// about a declined charge is never exposed.
/// </summary>
public sealed class InstallmentsReportDataSet : IndexedReportDataSet<InstallmentPlan, InstallmentPlanIndex, ReportChildRow<InstallmentPlan, InstallmentPlanPayment>>, ISubscriptionReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the plan a payment belongs to.
    /// </summary>
    public const string PlanIdField = "PlanId";

    /// <summary>
    /// The name of the field that holds when the plan was created, the data set's main date.
    /// </summary>
    public const string PlanCreatedUtcField = "PlanCreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session installment plans are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InstallmentsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<InstallmentsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.SubscriptionSource.Installments,
                stringLocalizer["Installments"],
                stringLocalizer["One row per payment of an installment plan, the down payment included: when it is due, its amount in the plan's currency, its status and its transaction."]),
            session,
            authorizationService,
            SubscriptionConstants.InstallmentPlanCollectionName,
            SubscriptionPermissions.ManageInstallmentPlans)
    {
        var S = stringLocalizer;
        var plan = S["Plan"].Value;
        var payment = S["Payment"].Value;

        AddField(PlanIdField, S["Plan ID"], DataFieldType.Text, row => row.Parent.ItemId, plan)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.SubscriptionSource.Name, CommerceReportDataSets.SubscriptionSource.InstallmentPlans));
        AddField("PlanTitle", S["Plan"], DataFieldType.Text, row => row.Parent.Title, plan);
        AddField("PlanStatus", S["Plan status"], DataFieldType.Text, row => row.Parent.Status, plan);
        AddField("Currency", S["Currency"], DataFieldType.Text, row => row.Parent.Currency, plan);
        AddField(InstallmentPlansReportDataSet.OwnerIdField, S["Customer user ID"], DataFieldType.Text, row => row.Parent.OwnerId, plan).Identifier(ReportDataReferences.User());
        AddField(PlanCreatedUtcField, S["Plan created"], DataFieldType.DateTime, row => row.Parent.CreatedUtc, plan);

        AddField(nameof(InstallmentPlanPayment.Number), S["Payment number"], DataFieldType.Integer, row => row.Child.Number, payment).Description = S["0 for the down payment, then 1, 2 and so on."].Value;
        AddField("IsDownPayment", S["Down payment"], DataFieldType.Boolean, row => row.Child.Number == 0, payment);
        AddField(nameof(InstallmentPlanPayment.DueUtc), S["Due"], DataFieldType.DateTime, row => row.Child.DueUtc, payment);
        AddField(nameof(InstallmentPlanPayment.Amount), S["Amount"], DataFieldType.Decimal, row => row.Child.Amount, payment).Description = S["In the plan's currency."].Value;
        AddField(nameof(InstallmentPlanPayment.Status), S["Status"], DataFieldType.Text, row => row.Child.Status, payment);
        AddField(nameof(InstallmentPlanPayment.PaidUtc), S["Received"], DataFieldType.DateTime, row => row.Child.PaidUtc, payment);
        AddField(nameof(InstallmentPlanPayment.TransactionId), S["Transaction ID"], DataFieldType.Text, row => row.Child.TransactionId, payment)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.TransactionSource.Name, CommerceReportDataSets.TransactionSource.Transactions));
        AddField(nameof(InstallmentPlanPayment.ChargeAttempts), S["Charge attempts"], DataFieldType.Integer, row => row.Child.ChargeAttempts, payment);
        AddField(nameof(InstallmentPlanPayment.LastChargeAttemptUtc), S["Last charged"], DataFieldType.DateTime, row => row.Child.LastChargeAttemptUtc, payment);
        AddField(nameof(InstallmentPlanPayment.NextChargeAttemptUtc), S["Next try"], DataFieldType.DateTime, row => row.Child.NextChargeAttemptUtc, payment);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<InstallmentPlanIndex, DateTime>> Column)? DateColumn
        => (PlanCreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<InstallmentPlanIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<InstallmentPlanIndex, string>>>(StringComparer.Ordinal)
        {
            [PlanIdField] = index => index.ItemId,
            [InstallmentPlansReportDataSet.OwnerIdField] = index => index.OwnerId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<InstallmentPlanIndex> columns)
    {
        columns
            .Enum("PlanStatus", index => index.Status)
            .Text("Currency", nameof(InstallmentPlanIndex.Currency));
    }

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<InstallmentPlan, InstallmentPlanPayment>> GetRows(InstallmentPlan document)
    {
        return ReportChildRow<InstallmentPlan, InstallmentPlanPayment>.For(document, document.Payments?.OrderBy(payment => payment.Number));
    }
}
