using System.Globalization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// The webhook events of Payroll, for an outside system that follows pay runs (an accounting or a benefits system).
/// </summary>
public static class PayrollWebhookEvents
{
    /// <summary>A pay run was approved.</summary>
    public const string PayRunApproved = "payroll.payrun.approved";

    /// <summary>A pay run was processed; its status says whether it is paid in full.</summary>
    public const string PayRunProcessed = "payroll.payrun.processed";

    /// <summary>A pay statement was issued.</summary>
    public const string PayStatementIssued = "payroll.statement.issued";

    /// <summary>A pay statement was voided.</summary>
    public const string PayStatementVoided = "payroll.statement.voided";
}

/// <summary>
/// Declares Payroll's webhook events and publishes them. A run's payload has its totals; a statement's has its ids and
/// dates but never a person's pay, tax id or bank account.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(WebhooksConstants.Feature.Webhooks)]
public sealed class PayrollWebhooksStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        var run = new { payRunId = "4qz...", number = "12", employerId = "4ab...", type = "Regular", periodStart = "2026-11-01", periodEnd = "2026-11-14", payDate = "2026-11-20", status = "Approved", statements = 25, gross = 61250.00m, net = 48120.55m, cashNeeded = 66012.40m };
        var statement = new { statementId = "4st...", number = "1042", payRunId = "4qz...", staffMemberId = "4sm...", employerId = "4ab...", payDate = "2026-11-20", status = "Paid", reversalId = (string)null };

        services.AddWebhookEvent(PayrollWebhookEvents.PayRunApproved, "Pay run approved", "Payroll", run);
        services.AddWebhookEvent(PayrollWebhookEvents.PayRunProcessed, "Pay run processed", "Payroll", run);
        services.AddWebhookEvent(PayrollWebhookEvents.PayStatementIssued, "Pay statement issued", "Payroll", statement);
        services.AddWebhookEvent(PayrollWebhookEvents.PayStatementVoided, "Pay statement voided", "Payroll", statement);

        services.TryAddScoped<PayrollWebhookObserver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayRunObserver, PayrollWebhookObserver>(provider => provider.GetRequiredService<PayrollWebhookObserver>()));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayStatementObserver, PayrollWebhookObserver>(provider => provider.GetRequiredService<PayrollWebhookObserver>()));
    }
}

/// <summary>
/// Publishes Payroll's webhook events from the run and statement observers.
/// </summary>
internal sealed class PayrollWebhookObserver : IPayRunObserver, IPayStatementObserver
{
    private readonly IWebhookPublisher _publisher;

    public PayrollWebhookObserver(IWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task ChangedAsync(PayRunChange change, CancellationToken cancellationToken = default)
    {
        var name = change.Kind switch
        {
            PayRunChangeKind.Approved => PayrollWebhookEvents.PayRunApproved,
            PayRunChangeKind.Paid or PayRunChangeKind.PartiallyPaid or PayRunChangeKind.Failed => PayrollWebhookEvents.PayRunProcessed,
            _ => null,
        };

        if (name is null)
        {
            return Task.CompletedTask;
        }

        var run = change.Run;

        return _publisher.PublishAsync(name, new
        {
            payRunId = run.ItemId,
            number = run.Number,
            employerId = run.EmployerId,
            type = run.Type.ToString(),
            periodStart = Day(run.PeriodStart),
            periodEnd = Day(run.PeriodEnd),
            payDate = Day(run.PayDate),
            status = run.Status.ToString(),
            statements = run.Totals.Statements,
            gross = run.Totals.Gross,
            net = run.Totals.Net,
            cashNeeded = run.Totals.CashNeeded,
        }, $"{name}:{run.ItemId}:{run.Version}:{run.Status}", cancellationToken);
    }

    public Task IssuedAsync(PayStatement statement, CancellationToken cancellationToken = default)
        => PublishAsync(PayrollWebhookEvents.PayStatementIssued, statement, null, cancellationToken);

    public Task VoidedAsync(PayStatement statement, PayStatement reversal, CancellationToken cancellationToken = default)
        => PublishAsync(PayrollWebhookEvents.PayStatementVoided, statement, reversal?.ItemId, cancellationToken);

    private Task PublishAsync(string name, PayStatement statement, string reversalId, CancellationToken cancellationToken)
        => _publisher.PublishAsync(name, new
        {
            statementId = statement.ItemId,
            number = statement.Number,
            payRunId = statement.RunId,
            staffMemberId = statement.StaffMemberId,
            employerId = statement.EmployerId,
            payDate = Day(statement.PayDate),
            status = statement.Status.ToString(),
            reversalId,
        }, $"{name}:{statement.ItemId}", cancellationToken);

    private static string Day(DateOnly day)
        => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
