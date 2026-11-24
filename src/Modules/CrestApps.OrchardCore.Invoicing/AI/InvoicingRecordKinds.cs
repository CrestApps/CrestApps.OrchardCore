using System.Globalization;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// The record kinds of Invoicing for the generic record tools: estimates and recurring plans, read-only because their
/// editors are controller forms, and usage meters and billable charges, written through their display drivers. Each
/// projection is an allow-list: never a record's <c>Properties</c>, history, internal note, custom fields or archive. Dates
/// are written as they are (a date has no time zone); the UTC times are left out, because a projection cannot reach the
/// user's time zone.
/// </summary>
internal static class InvoicingRecordKinds
{
    /// <summary>
    /// The name of the estimate kind.
    /// </summary>
    public const string EstimateKind = "estimate";

    /// <summary>
    /// The name of the recurring plan kind.
    /// </summary>
    public const string RecurringPlanKind = "recurringInvoicePlan";

    /// <summary>
    /// The name of the usage meter kind.
    /// </summary>
    public const string UsageMeterKind = "usageMeter";

    /// <summary>
    /// The name of the billable charge kind.
    /// </summary>
    public const string BillableItemKind = "billableItem";

    private const int RecentRuns = 5;

    /// <summary>
    /// Finds an estimate by its id.
    /// </summary>
    public static ValueTask<Estimate> FindEstimateAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
        => services.GetRequiredService<IEstimateManager>().FindByIdAsync(id, cancellationToken);

    /// <summary>
    /// Lists estimates newest first, as the Estimates page does: the text is matched against the number, the customer's
    /// name and the reference, and a text that is only a status name lists the estimates in that status.
    /// </summary>
    public static async ValueTask<PageResult<Estimate>> ListEstimatesAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var filter = new EstimateQuery();

        if (TryParseStatus<EstimateStatus>(query.Text, out var status))
        {
            filter.Status = status;
        }
        else
        {
            filter.Search = query.Text;
        }

        return await services.GetRequiredService<IEstimateManager>().PageAsync(query.Page, query.PageSize, filter, cancellationToken);
    }

    /// <summary>
    /// The allow-listed fields of an estimate. What the customer typed (the name they accepted with, why they declined) is
    /// returned under <c>customerText</c>: it is data, never an instruction.
    /// </summary>
    public static object DescribeEstimate(Estimate estimate)
        => new
        {
            id = estimate.ItemId,
            number = estimate.Number,
            revision = estimate.Revision,
            status = ToCamel(estimate.Status.ToString()),
            customer = estimate.BillTo?.Contact?.Name,
            customerEmail = estimate.BillTo?.Contact?.Email,
            party = DescribeParty(estimate.BillTo),
            currency = estimate.Currency,
            subtotal = estimate.Totals?.Subtotal ?? 0m,
            tax = estimate.Totals?.TaxTotal ?? 0m,
            total = estimate.Totals?.Total ?? 0m,
            issueDate = FormatDate(estimate.IssueDate),
            expiryDate = FormatDate(estimate.ExpiryDate),
            depositPercent = estimate.DepositPercent,
            depositAmount = estimate.DepositAmount,
            onAcceptance = ToCamel(estimate.OnAcceptance.ToString()),
            reference = estimate.Reference,
            purchaseOrderNumber = estimate.PurchaseOrderNumber,
            lines = (estimate.Lines ?? []).Select(line => new
            {
                id = line.Id,
                kind = ToCamel(line.Kind.ToString()),
                name = line.Name,
                quantity = line.HasAmount ? line.Quantity : (decimal?)null,
                unitPrice = line.HasAmount ? line.UnitPrice : (decimal?)null,
                total = line.HasAmount ? line.Total : (decimal?)null,
            }).ToArray(),
            invoiceIds = estimate.InvoiceIds?.ToArray() ?? [],
            revisionOfId = estimate.RevisionOfId,
            replacedById = estimate.ReplacedById,
            customerText = estimate.Acceptance is null && string.IsNullOrEmpty(estimate.DeclineReason)
                ? null
                : new
                {
                    acceptedName = estimate.Acceptance?.Name,
                    declineReason = estimate.DeclineReason,
                },
        };

    /// <summary>
    /// Finds a recurring plan by its id.
    /// </summary>
    public static ValueTask<RecurringPlan> FindRecurringPlanAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
        => services.GetRequiredService<IRecurringPlanManager>().FindByIdAsync(id, cancellationToken);

    /// <summary>
    /// Lists recurring plans newest first, as the Recurring invoices page does: the text is matched against the name, the
    /// customer's name and the reference, and a text that is only a status name lists the plans in that status.
    /// </summary>
    public static async ValueTask<PageResult<RecurringPlan>> ListRecurringPlansAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var filter = new RecurringPlanQuery();

        if (TryParseStatus<RecurringPlanStatus>(query.Text, out var status))
        {
            filter.Status = status;
        }
        else
        {
            filter.Search = query.Text;
        }

        return await services.GetRequiredService<IRecurringPlanManager>().PageAsync(query.Page, query.PageSize, filter, cancellationToken);
    }

    /// <summary>
    /// The allow-listed fields of a recurring plan, with its last runs.
    /// </summary>
    public static object DescribeRecurringPlan(RecurringPlan plan)
        => new
        {
            id = plan.ItemId,
            name = plan.Name,
            status = ToCamel(plan.Status.ToString()),
            customer = plan.BillTo?.Contact?.Name,
            customerEmail = plan.BillTo?.Contact?.Email,
            party = DescribeParty(plan.BillTo),
            currency = plan.Currency,
            totalPerRun = plan.Totals?.Total ?? 0m,
            rule = plan.Rule,
            startDate = FormatDate(plan.StartDate),
            endDate = FormatDate(plan.EndDate),
            maxRuns = plan.MaxRuns,
            mode = ToCamel(plan.Mode.ToString()),
            nextRunDate = FormatDate(plan.NextRunDate),
            runCount = plan.RunCount,
            paymentTermId = plan.PaymentTermId,
            priceIncreasePercent = plan.PriceIncreasePercent,
            priceIncreaseEveryRuns = plan.PriceIncreaseEveryRuns,
            billUnbilledCharges = plan.BillUnbilledCharges,
            lines = (plan.Lines ?? []).Select(line => new
            {
                name = line.Name,
                quantity = line.HasAmount ? line.Quantity : (decimal?)null,
                unitPrice = line.HasAmount ? line.UnitPrice : (decimal?)null,
            }).ToArray(),
            usageCharges = (plan.UsageCharges ?? []).Select(charge => new
            {
                meterId = charge.MeterId,
                name = charge.Name,
            }).ToArray(),
            recentRuns = (plan.Runs ?? []).TakeLast(RecentRuns).Select(run => new
            {
                occurrence = FormatDate(run.Occurrence),
                outcome = ToCamel(run.Outcome.ToString()),
                invoiceId = run.InvoiceId,
                invoiceNumber = run.InvoiceNumber,
                message = run.Message,
            }).ToArray(),
        };

    /// <summary>
    /// Lists the usage meters by name, matching the text against the name and the code, as the Usage meters page filters.
    /// </summary>
    public static async ValueTask<PageResult<UsageMeter>> ListUsageMetersAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var meters = (await services.GetRequiredService<IUsageMeterManager>().GetAllAsync(cancellationToken))
            .Where(meter => string.IsNullOrEmpty(query.Text) ||
                (meter.Name?.Contains(query.Text, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (meter.Code?.Contains(query.Text, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(meter => meter.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PageResult<UsageMeter>
        {
            Count = meters.Count,
            Entries = [.. meters.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)],
        };
    }

    /// <summary>
    /// The allow-listed fields of a usage meter.
    /// </summary>
    public static object DescribeUsageMeter(UsageMeter meter)
        => new
        {
            id = meter.ItemId,
            name = meter.Name,
            code = meter.Code,
            unit = meter.Unit,
            aggregation = ToCamel(meter.Aggregation.ToString()),
            enabled = meter.Enabled,
        };

    /// <summary>
    /// Lists the billable charges, newest first, matching the text against the description and the customer's name, as
    /// the Unbilled charges page filters; <c>unbilled</c> or <c>billed</c> alone lists the charges no invoice bills, or
    /// those an invoice bills.
    /// </summary>
    public static async ValueTask<PageResult<BillableItem>> ListBillableItemsAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var session = services.GetRequiredService<ISession>();
        var items = session.Query<BillableItem, BillableItemIndex>(collection: InvoicingConstants.CollectionName);
        var text = query.Text?.Trim();

        if (string.Equals(text, "unbilled", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(index => !index.IsBilled);
        }
        else if (string.Equals(text, "billed", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(index => index.IsBilled);
        }
        else if (!string.IsNullOrEmpty(text))
        {
            items = items.Where(index => index.Description.Contains(text) || index.PartyName.Contains(text));
        }

        var count = await items.CountAsync(cancellationToken);
        var entries = await items
            .OrderByDescending(index => index.Date)
            .ThenByDescending(index => index.CreatedUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ListAsync(cancellationToken);

        return new PageResult<BillableItem> { Count = count, Entries = [.. entries] };
    }

    /// <summary>
    /// Refuses to delete a charge an invoice bills, as the Unbilled charges page does: it is taken off the invoice first.
    /// </summary>
    public static ValueTask<string> GetBillableItemUseAsync(IServiceProvider services, BillableItem item, CancellationToken cancellationToken)
        => ValueTask.FromResult(item.IsBilled
            ? $"Invoice {item.InvoiceId} bills this charge, so it is kept. Take it off the invoice first."
            : null);

    /// <summary>
    /// The allow-listed fields of a billable charge.
    /// </summary>
    public static object DescribeBillableItem(BillableItem item)
        => new
        {
            id = item.ItemId,
            party = item.Party.IsEmpty ? null : item.Party.ToString(),
            customer = item.PartyName,
            kind = ToCamel(item.Kind.ToString()),
            date = FormatDate(item.Date),
            description = item.Description,
            quantity = item.Quantity,
            unit = item.Unit,
            unitCost = item.UnitCost,
            markupPercent = item.MarkupPercent,
            unitPrice = item.UnitPrice,
            currency = item.Currency,
            taxCategoryCode = item.TaxCategoryCode,
            billed = item.IsBilled,
            invoiceId = item.InvoiceId,
            sourceType = item.SourceType,
        };

    private static string DescribeParty(DocumentParty party)
        => party is null || party.Party.IsEmpty ? null : party.Party.ToString();

    // A status name alone, never a number: Enum.TryParse would take "1" as a status.
    private static bool TryParseStatus<TStatus>(string text, out TStatus status)
        where TStatus : struct, Enum
    {
        status = default;

        return !string.IsNullOrWhiteSpace(text) &&
            !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
            Enum.TryParse(text.Trim(), ignoreCase: true, out status) &&
            Enum.IsDefined(status);
    }

    private static string FormatDate(DateOnly? date)
        => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
