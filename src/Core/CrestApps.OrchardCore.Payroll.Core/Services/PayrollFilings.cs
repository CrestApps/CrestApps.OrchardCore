using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Notifications.Models;
using YesSql;
using YesSql.Indexes;
using YesSql.Services;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>Indexes tax deposits.</summary>
public sealed class PayrollTaxDepositIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the last day of the period, as a number.</summary>
    public int PeriodEndDay { get; set; }
}

/// <summary>Indexes filings.</summary>
public sealed class PayrollFilingIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the form.</summary>
    public string Form { get; set; }

    /// <summary>Gets or sets the year.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the quarter.</summary>
    public int Quarter { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public string Status { get; set; }
}

/// <summary>Maps deposits and filings to their index rows.</summary>
public sealed class PayrollFilingIndexProvider : IndexProvider<TaxDeposit>
{
    /// <summary>Initializes a new instance of the <see cref="PayrollFilingIndexProvider"/> class.</summary>
    public PayrollFilingIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TaxDeposit> context)
        => context
            .For<PayrollTaxDepositIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(deposit => new PayrollTaxDepositIndex
            {
                ItemId = deposit.ItemId,
                EmployerId = deposit.EmployerId,
                PeriodEndDay = PayrollFilingDays.Day(deposit.PeriodEnd),
            });
}

/// <summary>Maps filings to their index rows.</summary>
public sealed class PayrollFilingRecordIndexProvider : IndexProvider<PayrollFiling>
{
    /// <summary>Initializes a new instance of the <see cref="PayrollFilingRecordIndexProvider"/> class.</summary>
    public PayrollFilingRecordIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayrollFiling> context)
        => context
            .For<PayrollFilingIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(filing => new PayrollFilingIndex
            {
                ItemId = filing.ItemId,
                EmployerId = filing.EmployerId,
                Form = filing.Form,
                Year = filing.Year,
                Quarter = filing.Quarter,
                Status = filing.Status.ToString(),
            });
}

/// <summary>Days as numbers (yyyymmdd), as the statement index keeps them.</summary>
public static class PayrollFilingDays
{
    /// <summary>Gets a day as a number.</summary>
    /// <param name="day">The day.</param>
    /// <returns>The number.</returns>
    public static int Day(DateOnly day)
        => (day.Year * 10000) + (day.Month * 100) + day.Day;
}

/// <summary>Creates the tables of deposits and filings.</summary>
public static class PayrollFilingIndexSchema
{
    /// <summary>Creates the tables.</summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await CreateTaxDepositTableAsync(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<PayrollFilingIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EmployerId", column => column.Nullable().WithLength(26))
            .Column<string>("Form", column => column.Nullable().WithLength(50))
            .Column<int>("Year")
            .Column<int>("Quarter")
            .Column<string>("Status", column => column.Nullable().WithLength(20)),
            collection: PayrollConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<PayrollFilingIndex>(table => table
            .CreateIndex("IDX_PayrollFilingIndex_Employer", "EmployerId", "Form", "Year", "Quarter"),
            collection: PayrollConstants.CollectionName);
    }

    /// <summary>Creates the table of tax deposits.</summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateTaxDepositTableAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<PayrollTaxDepositIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EmployerId", column => column.Nullable().WithLength(26))
            .Column<int>("PeriodEndDay"),
            collection: PayrollConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<PayrollTaxDepositIndex>(table => table
            .CreateIndex("IDX_PayrollTaxDepositIndex_Employer", "EmployerId", "PeriodEndDay"),
            collection: PayrollConstants.CollectionName);
    }
}

/// <summary>Stores tax deposits.</summary>
public sealed class TaxDepositStore : DocumentCatalog<TaxDeposit, PayrollTaxDepositIndex>
{
    /// <summary>Initializes a new instance of the <see cref="TaxDepositStore"/> class.</summary>
    /// <param name="session">The session.</param>
    public TaxDepositStore(ISession session)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <summary>Lists deposits matching a filter.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The deposits.</returns>
    public async Task<IReadOnlyList<TaxDeposit>> ListAsync(System.Linq.Expressions.Expression<Func<PayrollTaxDepositIndex, bool>> filter, CancellationToken cancellationToken = default)
        => (await Session.Query<TaxDeposit, PayrollTaxDepositIndex>(filter, collection: CollectionName).ListAsync(cancellationToken)).ToList();
}

/// <summary>Stores filings.</summary>
public sealed class PayrollFilingStore : DocumentCatalog<PayrollFiling, PayrollFilingIndex>
{
    /// <summary>Initializes a new instance of the <see cref="PayrollFilingStore"/> class.</summary>
    /// <param name="session">The session.</param>
    public PayrollFilingStore(ISession session)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <summary>Lists filings matching a filter.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The filings.</returns>
    public async Task<IReadOnlyList<PayrollFiling>> ListAsync(System.Linq.Expressions.Expression<Func<PayrollFilingIndex, bool>> filter, CancellationToken cancellationToken = default)
        => (await Session.Query<PayrollFiling, PayrollFilingIndex>(filter, collection: CollectionName).ListAsync(cancellationToken)).ToList();
}

/// <summary>The default <see cref="ITaxDepositManager"/>.</summary>
public sealed class TaxDepositManager : CatalogManager<TaxDeposit>, ITaxDepositManager
{
    private readonly TaxDepositStore _store;

    /// <summary>Initializes a new instance of the <see cref="TaxDepositManager"/> class.</summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public TaxDepositManager(TaxDepositStore store, IEnumerable<ICatalogEntryHandler<TaxDeposit>> handlers, ILogger<CatalogManager<TaxDeposit>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaxDeposit>> ListAsync(string employerId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var fromDay = PayrollFilingDays.Day(from);
        var toDay = PayrollFilingDays.Day(to);

        return [.. (await _store.ListAsync(index => index.EmployerId == employerId && index.PeriodEndDay >= fromDay && index.PeriodEndDay <= toDay, cancellationToken)).OrderBy(deposit => deposit.PaidOn)];
    }
}

/// <summary>The default <see cref="IPayrollFilingManager"/>.</summary>
public sealed class PayrollFilingManager : CatalogManager<PayrollFiling>, IPayrollFilingManager
{
    private readonly PayrollFilingStore _store;

    /// <summary>Initializes a new instance of the <see cref="PayrollFilingManager"/> class.</summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public PayrollFilingManager(PayrollFilingStore store, IEnumerable<ICatalogEntryHandler<PayrollFiling>> handlers, ILogger<CatalogManager<PayrollFiling>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PayrollFiling>> ListByEmployersAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default)
    {
        if (employerIds is null)
        {
            return [.. await _store.GetAllAsync(cancellationToken)];
        }

        if (employerIds.Count == 0)
        {
            return [];
        }

        var ids = employerIds.ToArray();

        return await _store.ListAsync(index => index.EmployerId.IsIn(ids), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PayrollFiling> FindCurrentAsync(string employerId, string form, int year, int quarter, CancellationToken cancellationToken = default)
        => (await _store.ListAsync(index => index.EmployerId == employerId && index.Form == form && index.Year == year && index.Quarter == quarter, cancellationToken))
            .Where(filing => filing.Status != PayrollFilingStatus.Amended)
            .OrderByDescending(filing => filing.CreatedUtc)
            .FirstOrDefault();
}

/// <summary>
/// Checks deposits: an employer, a jurisdiction, a period, an amount above zero and the day it was paid.
/// </summary>
public sealed class TaxDepositHandler : CatalogEntryHandlerBase<TaxDeposit>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>Initializes a new instance of the <see cref="TaxDepositHandler"/> class.</summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxDepositHandler(IClock clock, IStringLocalizer<TaxDepositHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<TaxDeposit> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<TaxDeposit> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<TaxDeposit> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidatingAsync(ValidatingContext<TaxDeposit> context, CancellationToken cancellationToken = default)
    {
        var deposit = context.Model;

        if (string.IsNullOrEmpty(deposit.EmployerId) || string.IsNullOrEmpty(deposit.Jurisdiction))
        {
            context.Result.Fail(new ValidationResult(S["A deposit is of an employer's taxes to a jurisdiction."], [nameof(TaxDeposit.Jurisdiction)]));
        }

        if (deposit.PeriodStart == default || deposit.PeriodEnd < deposit.PeriodStart)
        {
            context.Result.Fail(new ValidationResult(S["Give the period the deposit pays."], [nameof(TaxDeposit.PeriodStart)]));
        }

        if (deposit.Amount <= 0)
        {
            context.Result.Fail(new ValidationResult(S["The amount is more than zero."], [nameof(TaxDeposit.Amount)]));
        }

        if (deposit.PaidOn == default)
        {
            context.Result.Fail(new ValidationResult(S["Give the day the deposit was made."], [nameof(TaxDeposit.PaidOn)]));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Stamps filings; a filed filing never changes again except to be marked amended.
/// </summary>
public sealed class PayrollFilingHandler : CatalogEntryHandlerBase<PayrollFiling>
{
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="PayrollFilingHandler"/> class.</summary>
    /// <param name="clock">The clock.</param>
    public PayrollFilingHandler(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<PayrollFiling> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<PayrollFiling> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<PayrollFiling> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }
}

/// <summary>
/// Works out the tax liabilities of an employer: the tax lines (both sides) of its final statements, grouped by
/// jurisdiction and group (withholding, unemployment) into the periods of the registration's deposit schedule
/// (unemployment by quarter), with the deposits recorded and the due date of each. Reversals count with their pay date.
/// </summary>
public sealed class TaxLiabilityService : ITaxLiabilityService
{
    private readonly PayStatementStore _statements;
    private readonly IEmployerManager _employers;
    private readonly ITaxDepositManager _deposits;

    /// <summary>Initializes a new instance of the <see cref="TaxLiabilityService"/> class.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="deposits">The deposits.</param>
    public TaxLiabilityService(PayStatementStore statements, IEmployerManager employers, ITaxDepositManager deposits)
    {
        _statements = statements;
        _employers = employers;
        _deposits = deposits;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaxLiabilityPeriod>> ListAsync(string employerId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var employer = await _employers.FindByIdAsync(employerId, cancellationToken);

        if (employer is null)
        {
            return [];
        }

        var fromDay = PayrollFilingDays.Day(from);
        var toDay = PayrollFilingDays.Day(to);
        var statements = (await _statements.ListAsync(index => index.EmployerId == employerId && index.PayDay >= fromDay && index.PayDay <= toDay, cancellationToken))
            .Where(statement => statement.Status is not PayStatementStatus.Draft);
        var periods = new Dictionary<string, TaxLiabilityPeriod>(StringComparer.Ordinal);

        foreach (var statement in statements)
        {
            foreach (var line in statement.Lines.Where(line => line.Type == PayLineType.Tax && !string.IsNullOrEmpty(line.Jurisdiction)))
            {
                // A tax another jurisdiction's agency collects is owed to that agency, with its own taxes.
                var jurisdiction = string.IsNullOrEmpty(line.RemittedTo) ? line.Jurisdiction : line.RemittedTo;
                var group = line.TaxCategory switch
                {
                    IncomeTaxCategories.Unemployment => TaxLiabilityGroups.Unemployment,

                    // Withheld from a contractor's payments: not a payroll tax, deposited and filed on its own.
                    IncomeTaxCategories.ContractorWithholding => TaxLiabilityGroups.NonPayrollWithholding,
                    _ => TaxLiabilityGroups.Withholding,
                };
                var schedule = group == TaxLiabilityGroups.Unemployment
                    ? DepositSchedules.Quarterly
                    : employer.Registrations.FirstOrDefault(registration => string.Equals(registration.Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(registration.DepositFrequency))?.DepositFrequency ?? DepositSchedules.Monthly;
                var (start, end, due) = Period(schedule, statement.PayDate);
                var period = new TaxLiabilityPeriod { EmployerId = employerId, Jurisdiction = jurisdiction, Group = group, Schedule = schedule, PeriodStart = start, PeriodEnd = end, DueDate = due, Currency = statement.Currency };

                if (!periods.TryGetValue(period.Key, out var existing))
                {
                    periods[period.Key] = existing = period;
                }

                if (line.Payer == PayLinePayer.Employer)
                {
                    existing.EmployerTaxes += line.Amount;
                }
                else
                {
                    existing.EmployeeTaxes += line.Amount;
                }
            }
        }

        foreach (var deposit in await _deposits.ListAsync(employerId, from, to.AddYears(1), cancellationToken))
        {
            var match = periods.Values.FirstOrDefault(period => string.Equals(period.Jurisdiction, deposit.Jurisdiction, StringComparison.OrdinalIgnoreCase) && period.Group == deposit.Group && period.PeriodStart == deposit.PeriodStart);

            if (match is not null)
            {
                match.Deposited += deposit.Amount;
            }
        }

        return [.. periods.Values.OrderBy(period => period.DueDate).ThenBy(period => period.Jurisdiction, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Gets the liability period of a pay date under a deposit schedule, and its due date: monthly by the 15th of the
    /// next month; semi-weekly, Wednesday to Friday by the next Wednesday and Saturday to Tuesday by the next Friday;
    /// quarterly by the last day of the month after the quarter, or by its 15th (Canada); twice a month by the 25th and
    /// the next 10th; four times a month by the third working day after each part; yearly by the last day of January.
    /// Due dates are not moved off weekends and holidays, but the working days of the four-times schedule skip weekends.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="payDate">The pay date.</param>
    /// <returns>The period and its due date.</returns>
    public static (DateOnly Start, DateOnly End, DateOnly Due) Period(string schedule, DateOnly payDate)
    {
        switch (schedule)
        {
            case DepositSchedules.SemiWeekly:
                {
                    // Wednesday to Friday, or Saturday to Tuesday.
                    var offset = ((int)payDate.DayOfWeek - (int)DayOfWeek.Wednesday + 7) % 7;
                    var midweek = offset <= 2;
                    var start = midweek ? payDate.AddDays(-offset) : payDate.AddDays(-(offset - 3));
                    var end = midweek ? start.AddDays(2) : start.AddDays(3);
                    var due = midweek ? end.AddDays(5) : end.AddDays(3);

                    return (start, end, due);
                }

            case DepositSchedules.Quarterly:
                {
                    var first = new DateOnly(payDate.Year, (((payDate.Month - 1) / 3) * 3) + 1, 1);
                    var last = first.AddMonths(3).AddDays(-1);

                    return (first, last, first.AddMonths(4).AddDays(-1));
                }

            case DepositSchedules.QuarterlyMidMonth:
                {
                    var first = new DateOnly(payDate.Year, (((payDate.Month - 1) / 3) * 3) + 1, 1);

                    return (first, first.AddMonths(3).AddDays(-1), first.AddMonths(3).AddDays(14));
                }

            case DepositSchedules.TwiceMonthly:
                {
                    var first = new DateOnly(payDate.Year, payDate.Month, 1);

                    return payDate.Day <= 15
                        ? (first, first.AddDays(14), first.AddDays(24))
                        : (first.AddDays(15), first.AddMonths(1).AddDays(-1), first.AddMonths(1).AddDays(9));
                }

            case DepositSchedules.FourTimesMonthly:
                {
                    var first = new DateOnly(payDate.Year, payDate.Month, 1);
                    var part = Math.Min(3, (payDate.Day - 1) / 7);
                    var start = first.AddDays(part * 7);
                    var end = part == 3 ? first.AddMonths(1).AddDays(-1) : start.AddDays(6);
                    var due = end;

                    for (var days = 0; days < 3;)
                    {
                        due = due.AddDays(1);

                        if (due.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                        {
                            days++;
                        }
                    }

                    return (start, end, due);
                }

            case DepositSchedules.Annual:
                return (new DateOnly(payDate.Year, 1, 1), new DateOnly(payDate.Year, 12, 31), new DateOnly(payDate.Year + 1, 1, 31));

            default:
                {
                    var first = new DateOnly(payDate.Year, payDate.Month, 1);

                    return (first, first.AddMonths(1).AddDays(-1), first.AddMonths(1).AddDays(14));
                }
        }
    }
}

/// <summary>
/// The default <see cref="IPayrollFilingService"/>.
/// </summary>
public sealed class PayrollFilingService : IPayrollFilingService
{
    private readonly IPayrollFilingManager _filings;
    private readonly IEnumerable<IPayrollFilingForm> _forms;
    private readonly PayStatementStore _statements;
    private readonly IPayCodeManager _codes;
    private readonly IPayProfileManager _profiles;
    private readonly ITaxDepositManager _deposits;
    private readonly IEmployerManager _employers;
    private readonly IPayRunManager _runs;
    private readonly IClock _clock;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>Initializes a new instance of the <see cref="PayrollFilingService"/> class.</summary>
    /// <param name="filings">The filings.</param>
    /// <param name="forms">The forms country packs add.</param>
    /// <param name="statements">The statements.</param>
    /// <param name="codes">The pay codes.</param>
    /// <param name="profiles">The pay profiles.</param>
    /// <param name="deposits">The deposits.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="runs">The pay runs.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="services">The services.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayrollFilingService(
        IPayrollFilingManager filings,
        IEnumerable<IPayrollFilingForm> forms,
        PayStatementStore statements,
        IPayCodeManager codes,
        IPayProfileManager profiles,
        ITaxDepositManager deposits,
        IEmployerManager employers,
        IPayRunManager runs,
        IClock clock,
        IServiceProvider services,
        IStringLocalizer<PayrollFilingService> stringLocalizer)
    {
        _filings = filings;
        _forms = forms;
        _statements = statements;
        _codes = codes;
        _profiles = profiles;
        _deposits = deposits;
        _employers = employers;
        _runs = runs;
        _clock = clock;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public IReadOnlyList<IPayrollFilingForm> FormsFor(Employer employer)
    {
        ArgumentNullException.ThrowIfNull(employer);

        return [.. _forms.Where(form => employer.Registrations.Any(registration => string.Equals(registration.Jurisdiction, form.Jurisdiction, StringComparison.OrdinalIgnoreCase)))];
    }

    /// <inheritdoc />
    public async Task<PayrollFilingResult> PrepareAsync(Employer employer, string form, int year, int quarter, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employer);

        var result = new PayrollFilingResult();
        var definition = FormsFor(employer).FirstOrDefault(candidate => candidate.Name == form);

        if (definition is null)
        {
            result.Errors.Add(S["The employer files no such form: register it in the form's jurisdiction first."]);

            return result;
        }

        if (definition.IsQuarterly ? quarter is < 1 or > 4 : quarter != 0)
        {
            result.Errors.Add(definition.IsQuarterly ? S["Choose the quarter."] : S["The form is for a whole year."]);

            return result;
        }

        var filing = await _filings.FindCurrentAsync(employer.ItemId, form, year, quarter, cancellationToken);

        if (filing?.Status == PayrollFilingStatus.Filed)
        {
            result.Errors.Add(S["The form was filed for {0}: amend it to correct it.", filing.Period]);

            return result;
        }

        var isNew = filing is null;

        if (isNew)
        {
            filing = await _filings.NewAsync(cancellationToken: cancellationToken);
            filing.EmployerId = employer.ItemId;
            filing.Form = form;
            filing.Year = year;
            filing.Quarter = quarter;
        }

        await FillAsync(employer, definition, filing, user, cancellationToken);

        if (isNew)
        {
            await _filings.CreateAsync(filing, cancellationToken);
        }
        else
        {
            await _filings.UpdateAsync(filing, cancellationToken: cancellationToken);
        }

        result.Filing = filing;
        await TellAsync(PayrollFilingChangeKind.Prepared, filing.EmployerId, filing.Year, filing, user, cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public async Task<PayrollFilingResult> MarkFiledAsync(PayrollFiling filing, DateOnly filedOn, string reference, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filing);

        var result = new PayrollFilingResult { Filing = filing };

        if (filing.Status != PayrollFilingStatus.Draft)
        {
            result.Errors.Add(S["Only a draft is marked as filed."]);

            return result;
        }

        if (filedOn == default)
        {
            result.Errors.Add(S["Give the day it was filed."]);

            return result;
        }

        filing.Status = PayrollFilingStatus.Filed;
        filing.FiledOn = filedOn;
        filing.Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        await _filings.UpdateAsync(filing, cancellationToken: cancellationToken);

        // The filing this one amends is now amended.
        if (!string.IsNullOrEmpty(filing.AmendsId) && await _filings.FindByIdAsync(filing.AmendsId, cancellationToken) is { } amended)
        {
            amended.Status = PayrollFilingStatus.Amended;
            await _filings.UpdateAsync(amended, cancellationToken: cancellationToken);
        }

        await TellAsync(PayrollFilingChangeKind.Filed, filing.EmployerId, filing.Year, filing, user, cancellationToken);

        // Each person with a statement in it is told their form is ready, with no amount.
        if (_forms.FirstOrDefault(form => form.Name == filing.Form) is { HasPersonStatements: true } definition)
        {
            foreach (var person in filing.People)
            {
                await StaffNotices.SendAsync(_services, person.StaffMemberId, new NotificationMessage
                {
                    Subject = S["Your {0} for {1} is ready", _services.GetService<IStringLocalizerFactory>()?.Localize(definition.DisplayName).Value ?? definition.Name, filing.Period],
                    Summary = S["Open My pay to see it and download it."],
                    TextBody = S["Your tax form is ready in My pay."],
                    Url = $"~/Admin/my-pay/tax-forms/{filing.ItemId}",
                    Type = PayrollConstants.TaxFormReadyNotificationType,
                    ReferenceId = filing.ItemId,
                }, cancellationToken);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PayrollFilingResult> AmendAsync(PayrollFiling filing, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filing);

        var result = new PayrollFilingResult();

        if (filing.Status != PayrollFilingStatus.Filed)
        {
            result.Errors.Add(S["Only a filed form is amended; prepare a draft again instead."]);

            return result;
        }

        var employer = await _employers.FindByIdAsync(filing.EmployerId, cancellationToken);
        var definition = employer is null ? null : FormsFor(employer).FirstOrDefault(candidate => candidate.Name == filing.Form);

        if (definition is null)
        {
            result.Errors.Add(S["The form is not available any more."]);

            return result;
        }

        var amendment = await _filings.NewAsync(cancellationToken: cancellationToken);

        amendment.EmployerId = filing.EmployerId;
        amendment.Form = filing.Form;
        amendment.Year = filing.Year;
        amendment.Quarter = filing.Quarter;
        amendment.AmendsId = filing.ItemId;
        await FillAsync(employer, definition, amendment, user, cancellationToken);
        await _filings.CreateAsync(amendment, cancellationToken);
        result.Filing = amendment;
        await TellAsync(PayrollFilingChangeKind.Amended, amendment.EmployerId, amendment.Year, amendment, user, cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> YearEndBlockersAsync(Employer employer, int year, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employer);

        var blockers = new List<string>();

        foreach (var run in (await _runs.ListByEmployersAsync([employer.ItemId], cancellationToken)).Where(run => run.PayDate.Year == year))
        {
            if (run.Status is not (PayRunStatus.Paid or PayRunStatus.Canceled))
            {
                blockers.Add(S["Pay run {0} (paid {1}) is {2}: process or cancel it.", run.Number, run.PayDate.ToString("d", System.Globalization.CultureInfo.CurrentCulture), run.Status.ToString()]);
            }
        }

        return blockers;
    }

    /// <inheritdoc />
    public async Task<PayrollFilingResult> CloseYearAsync(Employer employer, int year, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employer);

        var result = new PayrollFilingResult();

        foreach (var blocker in await YearEndBlockersAsync(employer, year, cancellationToken))
        {
            result.Errors.Add(blocker);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        // The year's wage statements, for each form that has a statement per person, when not filed yet.
        foreach (var form in FormsFor(employer).Where(form => form.HasPersonStatements && !form.IsQuarterly))
        {
            var current = await _filings.FindCurrentAsync(employer.ItemId, form.Name, year, 0, cancellationToken);

            if (current is null or { Status: PayrollFilingStatus.Draft })
            {
                var prepared = await PrepareAsync(employer, form.Name, year, 0, user, cancellationToken);

                foreach (var error in prepared.Errors)
                {
                    result.Errors.Add(error);
                }
            }
        }

        var fresh = await _employers.FindByIdAsync(employer.ItemId, cancellationToken) ?? employer;

        if (!fresh.ClosedYears.Contains(year))
        {
            fresh.ClosedYears.Add(year);
            await _employers.UpdateAsync(fresh, cancellationToken: cancellationToken);
            await TellAsync(PayrollFilingChangeKind.YearClosed, fresh.ItemId, year, null, user, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PayrollFilingResult> ReopenYearAsync(Employer employer, int year, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employer);

        var fresh = await _employers.FindByIdAsync(employer.ItemId, cancellationToken) ?? employer;

        if (fresh.ClosedYears.Remove(year))
        {
            await _employers.UpdateAsync(fresh, cancellationToken: cancellationToken);
            await TellAsync(PayrollFilingChangeKind.YearReopened, fresh.ItemId, year, null, user, cancellationToken);
        }

        return new PayrollFilingResult();
    }

    private async Task TellAsync(PayrollFilingChangeKind kind, string employerId, int year, PayrollFiling filing, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var change = new PayrollFilingChange
        {
            Kind = kind,
            EmployerId = employerId,
            Year = year,
            Filing = filing,
            Actor = user?.Identity?.IsAuthenticated == true ? user.Identity.Name : null,
            ActorId = user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null,
        };

        foreach (var observer in _services.GetServices<IPayrollFilingObserver>())
        {
            await observer.ChangedAsync(change, cancellationToken);
        }
    }

    private async Task FillAsync(Employer employer, IPayrollFilingForm definition, PayrollFiling filing, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (from, to) = definition.IsQuarterly
            ? (new DateOnly(filing.Year, ((filing.Quarter - 1) * 3) + 1, 1), new DateOnly(filing.Year, ((filing.Quarter - 1) * 3) + 1, 1).AddMonths(3).AddDays(-1))
            : (new DateOnly(filing.Year, 1, 1), new DateOnly(filing.Year, 12, 31));
        var employerId = employer.ItemId;
        var fromDay = PayrollFilingDays.Day(from);
        var toDay = PayrollFilingDays.Day(to);
        var statements = (await _statements.ListAsync(index => index.EmployerId == employerId && index.PayDay >= fromDay && index.PayDay <= toDay, cancellationToken))
            .Where(statement => statement.Status is not PayStatementStatus.Draft)
            .OrderBy(statement => statement.PayDate)
            .ToList();
        var profiles = new Dictionary<string, EmployeePayProfile>(StringComparer.Ordinal);

        foreach (var profileId in statements.Select(statement => statement.ProfileId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (await _profiles.FindByIdAsync(profileId, cancellationToken) is { } profile)
            {
                profiles[profileId] = profile;
            }
        }

        filing.Boxes.Clear();
        filing.People.Clear();
        filing.Notes.Clear();
        filing.PreparedBy = user?.Identity?.Name;
        filing.PreparedUtc = _clock.UtcNow;

        await definition.PrepareAsync(new PayrollFilingContext
        {
            Employer = employer,
            Filing = filing,
            From = from,
            To = to,
            Statements = statements,
            Codes = (await _codes.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal),
            Profiles = profiles,
            Deposits = await _deposits.ListAsync(employerId, from, to, cancellationToken),
            Services = _services,
        }, cancellationToken);
    }
}

/// <summary>
/// Registers deposits, filings, the liabilities and the filing service.
/// </summary>
public static class PayrollFilingsServiceCollectionExtensions
{
    /// <summary>Registers them.</summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayrollFilingsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TaxDepositStore>();
        services.TryAddScoped<PayrollFilingStore>();
        services.TryAddScoped<ITaxDepositManager, TaxDepositManager>();
        services.TryAddScoped<IPayrollFilingManager, PayrollFilingManager>();
        services.TryAddScoped<ICatalogManager<TaxDeposit>>(serviceProvider => serviceProvider.GetRequiredService<ITaxDepositManager>());
        services.TryAddScoped<ICatalogManager<PayrollFiling>>(serviceProvider => serviceProvider.GetRequiredService<IPayrollFilingManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PayrollFiling>, PayrollFilingHandler>());
        services.TryAddScoped<ITaxLiabilityService, TaxLiabilityService>();
        services.TryAddScoped<IPayrollFilingService, PayrollFilingService>();

        return services;
    }
}
