using System.Globalization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Recurrence;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NodaTime;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using IClock = OrchardCore.Modules.IClock;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The default <see cref="IRecurringPlanService"/>. A run makes one invoice per occurrence, found again by its source
/// (<c>RecurringPlan</c>, <c>{planId}:{occurrence}</c>), so a run repeated after a failure never makes a second one.
/// </summary>
public sealed partial class DefaultRecurringPlanService : IRecurringPlanService
{
    /// <summary>
    /// The prefix of the lock a plan command takes.
    /// </summary>
    public const string LockPrefix = "Invoicing:RecurringPlan:";

    private const int MaxRunsPerPlanPerSweep = 12;
    private const int PlansPerSweep = 100;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(5);

    private readonly IRecurringPlanStore _store;
    private readonly IInvoiceManager _invoices;
    private readonly IDocumentCalculator _calculator;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DefaultRecurringPlanService(
        IRecurringPlanStore store,
        IInvoiceManager invoices,
        IDocumentCalculator calculator,
        IDistributedLock distributedLock,
        ISession session,
        ISiteService siteService,
        IClock clock,
        IServiceProvider serviceProvider,
        ILogger<DefaultRecurringPlanService> logger,
        IStringLocalizer<DefaultRecurringPlanService> stringLocalizer)
    {
        _store = store;
        _invoices = invoices;
        _calculator = calculator;
        _distributedLock = distributedLock;
        _session = session;
        _siteService = siteService;
        _clock = clock;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<RecurringPlanResult> ActivateAsync(string planId, CancellationToken cancellationToken = default)
        => WithPlanAsync(planId, (plan, result) =>
        {
            if (plan.Status == RecurringPlanStatus.Active)
            {
                return Task.CompletedTask;
            }

            if (plan.Status != RecurringPlanStatus.Draft)
            {
                result.Fail(S["Only a draft plan can be started."]);

                return Task.CompletedTask;
            }

            var first = Next(plan, after: null);

            if (first is null)
            {
                result.Fail(S["The plan's schedule has no run within its end."]);

                return Task.CompletedTask;
            }

            plan.Status = RecurringPlanStatus.Active;
            plan.NextRunDate = first;

            return Task.CompletedTask;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<RecurringPlanResult> PauseAsync(string planId, CancellationToken cancellationToken = default)
        => WithPlanAsync(planId, (plan, result) =>
        {
            if (plan.Status != RecurringPlanStatus.Active)
            {
                result.Fail(S["Only an active plan can be paused."]);

                return Task.CompletedTask;
            }

            plan.Status = RecurringPlanStatus.Paused;

            return Task.CompletedTask;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<RecurringPlanResult> ResumeAsync(string planId, DateOnly today, CancellationToken cancellationToken = default)
        => WithPlanAsync(planId, (plan, result) =>
        {
            if (plan.Status != RecurringPlanStatus.Paused)
            {
                result.Fail(S["Only a paused plan can be resumed."]);

                return Task.CompletedTask;
            }

            // The runs missed while paused are skipped: the plan carries on from today.
            var next = plan.NextRunDate is { } due && due >= today ? due : Next(plan, after: today.AddDays(-1));

            plan.NextRunDate = next;
            plan.Status = next is null ? RecurringPlanStatus.Completed : RecurringPlanStatus.Active;

            return Task.CompletedTask;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<RecurringPlanResult> CancelAsync(string planId, CancellationToken cancellationToken = default)
        => WithPlanAsync(planId, (plan, result) =>
        {
            if (plan.Status == RecurringPlanStatus.Canceled)
            {
                return Task.CompletedTask;
            }

            plan.Status = RecurringPlanStatus.Canceled;
            plan.NextRunDate = null;

            return Task.CompletedTask;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<RecurringPlanResult> RunAsync(string planId, DateOnly today, CancellationToken cancellationToken = default)
        => WithPlanAsync(planId, async (plan, result) =>
        {
            if (plan.Status != RecurringPlanStatus.Active)
            {
                result.Fail(S["Only an active plan makes invoices."]);

                return;
            }

            var settings = await _siteService.GetSettingsAsync<InvoicingSettings>();

            for (var run = 0; run < MaxRunsPerPlanPerSweep && plan.NextRunDate is { } occurrence && occurrence <= today; run++)
            {
                await RunOccurrenceAsync(plan, occurrence, settings, result, cancellationToken);

                plan.NextRunDate = Next(plan, after: occurrence);

                if (plan.NextRunDate is null)
                {
                    plan.Status = RecurringPlanStatus.Completed;
                }
            }
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> RunDueAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var made = 0;

        foreach (var plan in await _store.ListDueAsync(today, PlansPerSweep, cancellationToken))
        {
            try
            {
                made += (await RunAsync(plan.ItemId, today, cancellationToken)).Invoices.Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The runs of recurring plan '{PlanId}' failed.", plan.ItemId);
            }
        }

        return made;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DateOnly> GetUpcoming(RecurringPlan plan, int count)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var upcoming = new List<DateOnly>();
        var from = plan.Status == RecurringPlanStatus.Draft ? Next(plan, after: null) : plan.NextRunDate;

        while (from is { } date && upcoming.Count < count)
        {
            upcoming.Add(date);
            from = Next(plan, after: date);
        }

        return upcoming;
    }

    // Makes the invoice of one occurrence and records the run on the plan.
    private async Task RunOccurrenceAsync(RecurringPlan plan, DateOnly occurrence, InvoicingSettings settings, RecurringPlanResult result, CancellationToken cancellationToken)
    {
        var sourceId = $"{plan.ItemId}:{occurrence.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        var record = new RecurringPlanRun { Occurrence = occurrence, RunUtc = _clock.UtcNow };

        var existing = await _invoices.PageAsync(1, 1, new InvoiceQuery { SourceType = InvoicingConstants.RecurringPlanReferenceType, SourceId = sourceId }, cancellationToken);

        if (existing.Entries.FirstOrDefault() is { } made)
        {
            record.Outcome = RecurringRunOutcome.Skipped;
            record.InvoiceId = made.ItemId;
            record.InvoiceNumber = made.Number;
            record.Message = S["This occurrence had an invoice already."].Value;
            plan.Runs.Add(record);

            return;
        }

        var occurrenceIndex = CountOccurrencesThrough(plan, occurrence);
        var periodEnd = PeriodEnd(plan, occurrence);
        var invoice = await _invoices.NewAsync(cancellationToken: cancellationToken);

        invoice.BillTo = new DocumentParty
        {
            Party = plan.BillTo?.Party ?? default,
            Contact = plan.BillTo?.Contact,
            ShippingAddress = plan.BillTo?.ShippingAddress,
            TaxId = plan.BillTo?.TaxId,
            TaxTreatment = plan.BillTo?.TaxTreatment ?? default,
            ExemptionReason = plan.BillTo?.ExemptionReason,
            CopyTo = [.. plan.BillTo?.CopyTo ?? []],
        };
        invoice.Currency = plan.Currency;
        invoice.PricesIncludeTax = plan.PricesIncludeTax;
        invoice.PaymentTermId = plan.PaymentTermId ?? settings.DefaultPaymentTermId;
        invoice.AllowPartialPayment = settings.AllowPartialPayment;
        invoice.AutoCharge = plan.Mode == RecurringPlanMode.IssueAndCharge;
        invoice.PurchaseOrderNumber = plan.PurchaseOrderNumber;
        invoice.Reference = plan.Reference ?? plan.Name;
        invoice.SourceType = InvoicingConstants.RecurringPlanReferenceType;
        invoice.SourceId = sourceId;
        invoice.ServiceStart = occurrence;
        invoice.ServiceEnd = periodEnd;
        invoice.IssueDate = plan.Mode == RecurringPlanMode.Draft ? null : occurrence;
        invoice.Notes = RenderPeriod(plan.Notes, occurrence, periodEnd);
        invoice.Terms = plan.Terms;
        invoice.Footer = plan.Footer;
        invoice.TemplateId = plan.TemplateId;
        invoice.Charges = [.. plan.Charges];

        var factor = PriceFactor(plan, occurrenceIndex);

        foreach (var line in plan.Lines)
        {
            invoice.Lines.Add(new DocumentLine
            {
                Id = IdGenerator.GenerateId(),
                Kind = line.Kind,
                ProductId = line.ProductId,
                Sku = line.Sku,
                Name = RenderPeriod(line.Name, occurrence, periodEnd),
                Description = RenderPeriod(line.Description, occurrence, periodEnd),
                Quantity = line.Quantity,
                Unit = line.Unit,
                UnitPrice = factor == 1m ? line.UnitPrice : Math.Round(line.UnitPrice * factor, 2, MidpointRounding.AwayFromZero),
                DiscountPercent = line.DiscountPercent,
                DiscountAmount = line.DiscountAmount,
                TaxCategoryCode = line.TaxCategoryCode,
                Taxes = [.. line.Taxes ?? []],
                TaxTreatment = line.TaxTreatment,
                TaxTreatmentReason = line.TaxTreatmentReason,
                ServiceStart = occurrence,
                ServiceEnd = periodEnd,
                SourceType = InvoicingConstants.RecurringPlanReferenceType,
                SourceId = plan.ItemId,
            });
        }

        // The usage of the period that just ended, priced by each charge's tiers.
        if (plan.UsageCharges?.Count > 0)
        {
            var usageError = await AddUsageAsync(plan, invoice, occurrence, cancellationToken);

            if (usageError is not null)
            {
                await FailAsync(plan, record, usageError, result);

                return;
            }

            // A plan that bills only usage makes no invoice for a period without any.
            if (!invoice.Lines.Any(line => line.Kind is not (DocumentLineKind.Text or DocumentLineKind.Section)))
            {
                record.Outcome = RecurringRunOutcome.Skipped;
                record.Message = S["There was no usage to bill."].Value;
                plan.RunCount++;
                plan.Runs.Add(record);

                return;
            }
        }

        // The customer's charges kept to bill later, up to the run's day, when the plan bills them too.
        if (plan.BillUnbilledCharges && _serviceProvider.GetService<IBillingService>() is { } billing)
        {
            var charges = await billing.ListUnbilledAsync(ChargesBillableSource.SourceType, new BillableQuery { Party = invoice.BillTo.Party, Currency = invoice.Currency, Through = occurrence }, cancellationToken);

            await billing.AddToDraftAsync(invoice, ChargesBillableSource.SourceType, charges, cancellationToken);
        }

        await InvoiceCalculation.ApplyAsync(_calculator, invoice, settings, issueDate: null, cancellationToken);

        var validation = await _invoices.ValidateAsync(invoice, cancellationToken);

        if (!validation.Succeeded)
        {
            // The draft is not kept, so the charges it took are let go.
            if (invoice.BilledItems.Count > 0 && _serviceProvider.GetService<IBillingService>() is { } releasing)
            {
                await releasing.ReconcileAsync(invoice, releaseAll: true, cancellationToken);
            }

            await FailAsync(plan, record, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)), result);

            return;
        }

        await _invoices.CreateAsync(invoice, cancellationToken);

        // The issue reads the draft under its lock in a unit of work of its own, so the draft is written first.
        await _session.SaveChangesAsync(cancellationToken);

        record.InvoiceId = invoice.ItemId;
        record.Outcome = RecurringRunOutcome.Drafted;

        if (plan.Mode != RecurringPlanMode.Draft)
        {
            // Issued and sent in a unit of work of their own: a refused issue cancels its session, which must not take the
            // plan's run record with it.
            await using var scope = _serviceProvider.CreateAsyncScope();

            var issued = await scope.ServiceProvider.GetRequiredService<IInvoiceCommandService>().IssueAsync(invoice.ItemId, cancellationToken);

            if (!issued.Succeeded)
            {
                await FailAsync(plan, record, S["The invoice was made but not issued: {0}", string.Join(" ", issued.Errors)].Value, result);

                return;
            }

            invoice = issued.Invoice;
            record.InvoiceNumber = invoice.Number;
            record.Outcome = RecurringRunOutcome.Issued;

            if (plan.Mode == RecurringPlanMode.IssueAndSend && scope.ServiceProvider.GetService<IInvoiceSender>() is { } sender)
            {
                var sent = await sender.SendAsync(new InvoiceSendRequest { InvoiceId = invoice.ItemId }, cancellationToken);

                if (sent.Succeeded)
                {
                    record.Outcome = RecurringRunOutcome.Sent;
                }
                else
                {
                    record.Message = S["Issued but not sent: {0}", string.Join(" ", sent.Errors)].Value;
                }
            }
        }

        plan.RunCount++;
        plan.Runs.Add(record);
        result.Invoices.Add(invoice);
        _pending.Add(observer => observer.GeneratedAsync(plan, invoice, record));
    }

    // Adds a line per usage band of each usage charge, for the period from the previous occurrence up to the day before
    // this one (none on the first run, which has no period behind it). Returns what went wrong, or nothing.
    private async Task<string> AddUsageAsync(RecurringPlan plan, Invoice invoice, DateOnly occurrence, CancellationToken cancellationToken)
    {
        var usage = _serviceProvider.GetService<IUsageService>();
        var meters = _serviceProvider.GetService<IUsageMeterManager>();

        if (usage is null || meters is null)
        {
            return S["The plan bills usage, but Invoicing Billing is not on."].Value;
        }

        if (Previous(plan, occurrence) is not { } periodStart)
        {
            return null;
        }

        var periodEnd = occurrence.AddDays(-1);
        var localClock = _serviceProvider.GetRequiredService<ILocalClock>();
        var fromUtc = DateTime.SpecifyKind(await localClock.ToUtcLenientAsync(periodStart.ToDateTime(TimeOnly.MinValue)), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(await localClock.ToUtcLenientAsync(occurrence.ToDateTime(TimeOnly.MinValue)), DateTimeKind.Utc);
        var culture = CultureInfo.CurrentCulture;

        foreach (var charge in plan.UsageCharges)
        {
            var meter = await meters.FindByIdAsync(charge.MeterId, cancellationToken);

            if (meter is null)
            {
                return S["A usage charge's meter could not be found. Choose another on the plan."].Value;
            }

            var quantity = await usage.GetTotalAsync(meter.ItemId, invoice.BillTo.Party, fromUtc, toUtc, cancellationToken);
            var bands = UsagePricing.Price(charge.TierMode, charge.Tiers, quantity);
            var name = RenderPeriod(string.IsNullOrWhiteSpace(charge.Name) ? meter.Name : charge.Name, periodStart, periodEnd);

            foreach (var band in bands)
            {
                invoice.Lines.Add(new DocumentLine
                {
                    Id = IdGenerator.GenerateId(),
                    Kind = DocumentLineKind.Usage,
                    Name = UsagePricing.Describe(name, band, bands.Count, culture),
                    Quantity = band.Quantity,
                    Unit = meter.Unit,
                    UnitPrice = band.UnitPrice,
                    TaxCategoryCode = charge.TaxCategoryCode,
                    ServiceStart = periodStart,
                    ServiceEnd = periodEnd,
                    SourceType = InvoicingConstants.UsageMeterReferenceType,
                    SourceId = meter.ItemId,
                });
            }
        }

        return null;
    }

    private Task FailAsync(RecurringPlan plan, RecurringPlanRun record, string message, RecurringPlanResult result)
    {
        record.Outcome = RecurringRunOutcome.Failed;
        record.Message = message;
        plan.Runs.Add(record);
        _pending.Add(observer => observer.RunFailedAsync(plan, record));

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Recurring plan '{PlanId}' failed its run of {Occurrence}: {Message}", plan.ItemId, record.Occurrence, message);
        }

        return Task.CompletedTask;
    }

    private readonly List<Func<IRecurringPlanObserver, Task>> _pending = [];

    // Runs one command under the plan's lock, saves the plan, and tells the observers what the runs did.
    private async Task<RecurringPlanResult> WithPlanAsync(string planId, Func<RecurringPlan, RecurringPlanResult, Task> command, CancellationToken cancellationToken)
    {
        var result = new RecurringPlanResult();

        if (string.IsNullOrEmpty(planId))
        {
            return result.Fail(S["The plan could not be found."]);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + planId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return result.Fail(S["This plan is busy. Try again in a moment."]);
        }

        await using var _ = locker;

        var plan = await _store.FindByIdAsync(planId, cancellationToken);

        if (plan is null)
        {
            return result.Fail(S["The plan could not be found."]);
        }

        result.Plan = plan;
        _pending.Clear();

        await command(plan, result);

        if (!result.Succeeded && result.Invoices.Count == 0 && _pending.Count == 0)
        {
            return result;
        }

        plan.ModifiedUtc = _clock.UtcNow;

        try
        {
            await _store.UpdateAsync(plan, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await _session.CancelAsync();

            return result.Fail(S["This plan was changed by someone else meanwhile. Reload it and try again."]);
        }

        foreach (var notify in _pending.ToArray())
        {
            foreach (var observer in _serviceProvider.GetServices<IRecurringPlanObserver>())
            {
                try
                {
                    await notify(observer);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "A recurring plan observer '{Observer}' failed on plan '{PlanId}'.", observer.GetType().FullName, plan.ItemId);
                }
            }
        }

        _pending.Clear();

        return result;
    }

    /// <summary>
    /// The first occurrence of the plan's schedule on or after a day, within its end date and number of runs.
    /// </summary>
    public static DateOnly? NextOnOrAfter(RecurringPlan plan, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return Next(plan, after: day.AddDays(-1));
    }

    // The first occurrence after a day (or the first of all), within the plan's end date and number of runs.
    internal static DateOnly? Next(RecurringPlan plan, DateOnly? after)
    {
        if (!RecurrenceRule.TryParse(plan.Rule, out var rule, out _))
        {
            return null;
        }

        var index = 0;

        foreach (var occurrence in RecurrenceExpander.GetOccurrences(rule, LocalDate.FromDateOnly(plan.StartDate).AtMidnight(), DateTimeZone.Utc))
        {
            var date = occurrence.Date.ToDateOnly();
            index++;

            if (plan.MaxRuns is { } max && index > max || plan.EndDate is { } end && date > end)
            {
                return null;
            }

            if (after is null || date > after.Value)
            {
                return date;
            }

            if (index > RecurrenceExpander.MaxPeriods)
            {
                return null;
            }
        }

        return null;
    }

    // The occurrence of the plan's schedule before a day, or nothing for its first.
    private static DateOnly? Previous(RecurringPlan plan, DateOnly occurrence)
    {
        if (!RecurrenceRule.TryParse(plan.Rule, out var rule, out _))
        {
            return null;
        }

        DateOnly? previous = null;
        var index = 0;

        foreach (var item in RecurrenceExpander.GetOccurrences(rule, LocalDate.FromDateOnly(plan.StartDate).AtMidnight(), DateTimeZone.Utc))
        {
            var date = item.Date.ToDateOnly();

            if (date >= occurrence || ++index > RecurrenceExpander.MaxPeriods)
            {
                break;
            }

            previous = date;
        }

        return previous;
    }

    private static int CountOccurrencesThrough(RecurringPlan plan, DateOnly occurrence)
    {
        if (!RecurrenceRule.TryParse(plan.Rule, out var rule, out _))
        {
            return 1;
        }

        var index = 0;

        foreach (var item in RecurrenceExpander.GetOccurrences(rule, LocalDate.FromDateOnly(plan.StartDate).AtMidnight(), DateTimeZone.Utc))
        {
            index++;

            if (item.Date.ToDateOnly() >= occurrence || index > RecurrenceExpander.MaxPeriods)
            {
                break;
            }
        }

        return index;
    }

    // The period an occurrence bills: up to the day before the next occurrence of the rule, or the day itself.
    private static DateOnly PeriodEnd(RecurringPlan plan, DateOnly occurrence)
    {
        if (!RecurrenceRule.TryParse(plan.Rule, out var rule, out _))
        {
            return occurrence;
        }

        foreach (var item in RecurrenceExpander.GetOccurrences(rule, LocalDate.FromDateOnly(plan.StartDate).AtMidnight(), DateTimeZone.Utc))
        {
            var date = item.Date.ToDateOnly();

            if (date > occurrence)
            {
                return date.AddDays(-1);
            }
        }

        return occurrence;
    }

    private static decimal PriceFactor(RecurringPlan plan, int occurrenceIndex)
    {
        if (plan.PriceIncreasePercent is not > 0m || plan.PriceIncreaseEveryRuns is not > 0)
        {
            return 1m;
        }

        var rises = (occurrenceIndex - 1) / plan.PriceIncreaseEveryRuns.Value;
        var factor = 1m;

        for (var i = 0; i < rises; i++)
        {
            factor *= 1m + (plan.PriceIncreasePercent.Value / 100m);
        }

        return factor;
    }

    /// <summary>
    /// Writes the period into text: <c>{{ period.start }}</c>, <c>{{ period.end }}</c>, <c>{{ period.month }}</c> (the
    /// month's name), <c>{{ period.year }}</c>, and <c>{{ period.start | date: "MMMM yyyy" }}</c> with a .NET date format.
    /// </summary>
    public static string RenderPeriod(string text, DateOnly start, DateOnly end)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{", StringComparison.Ordinal))
        {
            return text;
        }

        var culture = CultureInfo.CurrentCulture;

        return PeriodPlaceholder().Replace(text, match =>
        {
            var date = match.Groups["part"].Value switch
            {
                "start" or "month" or "year" => start,
                "end" => end,
                _ => (DateOnly?)null,
            };

            if (date is null)
            {
                return match.Value;
            }

            if (match.Groups["format"].Success)
            {
                return date.Value.ToString(match.Groups["format"].Value, culture);
            }

            return match.Groups["part"].Value switch
            {
                "month" => date.Value.ToString("MMMM", culture),
                "year" => date.Value.Year.ToString(CultureInfo.InvariantCulture),
                _ => date.Value.ToString("d", culture),
            };
        });
    }

    [GeneratedRegex("""\{\{\s*period\.(?<part>start|end|month|year)\s*(\|\s*date:\s*["'](?<format>[^"']+)["']\s*)?\}\}""")]
    private static partial Regex PeriodPlaceholder();
}
