using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;

namespace CrestApps.OrchardCore.Payroll.Workflows;

/// <summary>
/// Starts the garnishment events once the change is committed: what an order withheld and sent to its payee, a
/// remittance voided with its statement, and an order completed.
/// </summary>
internal sealed class GarnishmentsWorkflowObserver : IGarnishmentObserver
{
    /// <inheritdoc />
    public Task ChangedAsync(GarnishmentChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (change.Order is null)
        {
            return Task.CompletedTask;
        }

        PayrollWorkflowObserver.Trigger([EventFor(change.Kind)], RemittanceInput(change), change.Order.ItemId);

        return Task.CompletedTask;
    }

    internal static string EventFor(GarnishmentChangeKind kind)
        => kind switch
        {
            GarnishmentChangeKind.Withheld => nameof(GarnishmentWithheldEvent),
            GarnishmentChangeKind.RemittanceVoided => nameof(GarnishmentRemittanceVoidedEvent),
            GarnishmentChangeKind.Completed => nameof(GarnishmentOrderCompletedEvent),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    internal static Dictionary<string, object> RemittanceInput(GarnishmentChange change)
    {
        var order = change.Order;
        var remittance = change.Remittance;

        return new Dictionary<string, object>
        {
            ["OrderId"] = order.ItemId,
            ["EmployerId"] = order.EmployerId,
            ["StaffMemberId"] = order.StaffMemberId,
            ["Type"] = order.Type.ToString(),
            ["CaseNumber"] = order.CaseNumber,
            ["Agency"] = order.Agency,
            ["PayeeName"] = order.PayeeName,
            ["Status"] = order.Status.ToString(),
            ["Withheld"] = order.Withheld,
            ["TotalOwed"] = order.TotalOwed,
            ["Remaining"] = order.Remaining,
            ["StatementId"] = change.Statement?.ItemId ?? remittance?.StatementId,
            ["StatementNumber"] = change.Statement?.Number ?? remittance?.StatementNumber,
            ["PayDate"] = remittance is null ? null : PayrollWorkflowObserver.Day(remittance.PayDate),
            ["Amount"] = remittance?.Amount,
            ["Currency"] = change.Statement?.Currency,
            ["Method"] = remittance?.Method.ToString(),
            ["ReferenceId"] = remittance?.ReferenceId,
            ["Reference"] = remittance?.Reference,
            ["Problem"] = remittance?.Problem,
            ["Change"] = change.Kind.ToString(),
        };
    }
}

/// <summary>
/// Starts Garnishment Order Created when an order is added, by hand or by a recipe.
/// </summary>
internal sealed class GarnishmentOrderWorkflowHandler : CatalogEntryHandlerBase<GarnishmentOrder>
{
    /// <inheritdoc />
    public override Task CreatedAsync(CreatedContext<GarnishmentOrder> context, CancellationToken cancellationToken = default)
    {
        PayrollWorkflowObserver.Trigger([nameof(GarnishmentOrderCreatedEvent)], OrderInput(context.Model), context.Model.ItemId);

        return Task.CompletedTask;
    }

    internal static Dictionary<string, object> OrderInput(GarnishmentOrder order)
        => new()
        {
            ["OrderId"] = order.ItemId,
            ["EmployerId"] = order.EmployerId,
            ["StaffMemberId"] = order.StaffMemberId,
            ["ProfileId"] = order.ProfileId,
            ["Type"] = order.Type.ToString(),
            ["CaseNumber"] = order.CaseNumber,
            ["Agency"] = order.Agency,
            ["Status"] = order.Status.ToString(),
            ["TotalOwed"] = order.TotalOwed,
            ["PayeeName"] = order.PayeeName,
            ["PaymentMethod"] = order.PaymentMethod.ToString(),
            ["StartDate"] = order.StartDate == default ? null : PayrollWorkflowObserver.Day(order.StartDate),
        };
}

/// <summary>
/// Starts Tax Deposit Recorded when a tax deposit is recorded, by hand or by a recipe.
/// </summary>
internal sealed class TaxDepositWorkflowHandler : CatalogEntryHandlerBase<TaxDeposit>
{
    /// <inheritdoc />
    public override Task CreatedAsync(CreatedContext<TaxDeposit> context, CancellationToken cancellationToken = default)
    {
        PayrollWorkflowObserver.Trigger([nameof(TaxDepositRecordedEvent)], DepositInput(context.Model), context.Model.ItemId);

        return Task.CompletedTask;
    }

    internal static Dictionary<string, object> DepositInput(TaxDeposit deposit)
        => new()
        {
            ["DepositId"] = deposit.ItemId,
            ["EmployerId"] = deposit.EmployerId,
            ["Jurisdiction"] = deposit.Jurisdiction,
            ["Group"] = deposit.Group,
            ["PeriodStart"] = PayrollWorkflowObserver.Day(deposit.PeriodStart),
            ["PeriodEnd"] = PayrollWorkflowObserver.Day(deposit.PeriodEnd),
            ["Amount"] = deposit.Amount,
            ["PaidOn"] = PayrollWorkflowObserver.Day(deposit.PaidOn),
            ["Reference"] = deposit.Reference,
            ["RecordedBy"] = deposit.RecordedBy,
        };
}
