using System.Security.Claims;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Indexes tax returns.
/// </summary>
public sealed class TaxReturnIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the registration.
    /// </summary>
    public string RegistrationId { get; set; }

    /// <summary>
    /// Gets or sets the form.
    /// </summary>
    public string Form { get; set; }

    /// <summary>
    /// Gets or sets the period key.
    /// </summary>
    public string PeriodKey { get; set; }

    /// <summary>
    /// Gets or sets the last day of the period, as a number (yyyymmdd).
    /// </summary>
    public int PeriodEndDay { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }
}

/// <summary>
/// Maps tax returns to <see cref="TaxReturnIndex"/> rows.
/// </summary>
public sealed class TaxReturnIndexProvider : IndexProvider<TaxReturn>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxReturnIndexProvider"/> class.
    /// </summary>
    public TaxReturnIndexProvider()
    {
        CollectionName = TaxationConstants.TaxReturnsCollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TaxReturn> context)
        => context
            .For<TaxReturnIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(taxReturn => new TaxReturnIndex
            {
                ItemId = taxReturn.ItemId,
                RegistrationId = taxReturn.RegistrationId,
                Form = taxReturn.Form,
                PeriodKey = taxReturn.PeriodKey,
                PeriodEndDay = TaxReturnDays.Day(taxReturn.PeriodEnd),
                Status = taxReturn.Status.ToString(),
            });
}

/// <summary>
/// Days as numbers (yyyymmdd), so a span of days is a range of the index.
/// </summary>
public static class TaxReturnDays
{
    /// <summary>
    /// Gets a day as a number.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>The number.</returns>
    public static int Day(DateOnly day)
        => (day.Year * 10000) + (day.Month * 100) + day.Day;
}

/// <summary>
/// Creates the table of tax returns.
/// </summary>
public static class TaxReturnIndexSchema
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<TaxReturnIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("RegistrationId", column => column.Nullable().WithLength(26))
            .Column<string>("Form", column => column.Nullable().WithLength(100))
            .Column<string>("PeriodKey", column => column.Nullable().WithLength(10))
            .Column<int>("PeriodEndDay")
            .Column<string>("Status", column => column.Nullable().WithLength(20)),
            collection: TaxationConstants.TaxReturnsCollectionName);

        await schemaBuilder.AlterIndexTableAsync<TaxReturnIndex>(table => table
            .CreateIndex("IDX_TaxReturnIndex_Period", "RegistrationId", "Form", "PeriodKey"),
            collection: TaxationConstants.TaxReturnsCollectionName);

        await schemaBuilder.AlterIndexTableAsync<TaxReturnIndex>(table => table
            .CreateIndex("IDX_TaxReturnIndex_PeriodEnd", "PeriodEndDay", "DocumentId"),
            collection: TaxationConstants.TaxReturnsCollectionName);
    }
}

/// <summary>
/// Stores tax returns in the Tax Returns collection.
/// </summary>
public sealed class TaxReturnStore : DocumentCatalog<TaxReturn, TaxReturnIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxReturnStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public TaxReturnStore(ISession session)
        : base(session)
    {
        CollectionName = TaxationConstants.TaxReturnsCollectionName;
    }

    /// <summary>
    /// Lists the returns matching a filter.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The returns.</returns>
    public async Task<IReadOnlyList<TaxReturn>> ListAsync(System.Linq.Expressions.Expression<Func<TaxReturnIndex, bool>> filter, CancellationToken cancellationToken = default)
        => [.. await Session.Query<TaxReturn, TaxReturnIndex>(filter, collection: CollectionName).ListAsync(cancellationToken)];

    /// <summary>
    /// Counts the returns matching a filter.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count.</returns>
    public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<TaxReturnIndex, bool>> filter, CancellationToken cancellationToken = default)
        => Session.QueryIndex<TaxReturnIndex>(filter, collection: CollectionName).CountAsync(cancellationToken);
}

/// <summary>
/// The default <see cref="ITaxReturnManager"/>.
/// </summary>
public sealed class TaxReturnManager : CatalogManager<TaxReturn>, ITaxReturnManager
{
    private readonly TaxReturnStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxReturnManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public TaxReturnManager(TaxReturnStore store, IEnumerable<ICatalogEntryHandler<TaxReturn>> handlers, ILogger<CatalogManager<TaxReturn>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TaxReturn>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var fromDay = TaxReturnDays.Day(from);
        var toDay = TaxReturnDays.Day(to);

        return _store.ListAsync(index => index.PeriodEndDay >= fromDay && index.PeriodEndDay <= toDay, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasReturnsAsync(string registrationId, CancellationToken cancellationToken = default)
        => !string.IsNullOrEmpty(registrationId) && await _store.CountAsync(index => index.RegistrationId == registrationId, cancellationToken) > 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaxReturn>> ListVersionsAsync(string registrationId, string form, string periodKey, CancellationToken cancellationToken = default)
        => [.. (await _store.ListAsync(index => index.RegistrationId == registrationId && index.Form == form && index.PeriodKey == periodKey, cancellationToken))
            .OrderBy(taxReturn => taxReturn.Amendment)];

    /// <inheritdoc />
    public async Task<TaxReturn> FindCurrentAsync(string registrationId, string form, string periodKey, CancellationToken cancellationToken = default)
        => (await ListVersionsAsync(registrationId, form, periodKey, cancellationToken))
            .Where(taxReturn => taxReturn.Status != TaxReturnStatus.Amended)
            .OrderByDescending(taxReturn => taxReturn.Amendment)
            .FirstOrDefault();
}

/// <summary>
/// Stamps tax returns when they are added and changed.
/// </summary>
public sealed class TaxReturnHandler : CatalogEntryHandlerBase<TaxReturn>
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxReturnHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public TaxReturnHandler(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<TaxReturn> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<TaxReturn> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<TaxReturn> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }
}

/// <summary>
/// The built-in form any registration can file with: the lines summed by kind. Its boxes are <c>GrossSales</c>,
/// <c>ExemptSales</c>, <c>TaxableSales</c>, <c>SalesTax</c>, <c>UseTaxPurchases</c>, <c>UseTax</c>, <c>Withholding</c>,
/// <c>ExcessCollected</c>, <c>TotalTax</c>, <c>Credits</c> and <c>AmountDue</c>. It has no discount, penalty or interest;
/// a jurisdiction's own form adds them.
/// </summary>
public sealed class GenericTaxReturnForm : ITaxReturnForm
{
    /// <summary>
    /// The stored name.
    /// </summary>
    public const string FormName = "Generic";

    private static readonly LocalizationSource _displayName = LocalizationSource.Create<GenericTaxReturnForm>("Generic return (totals by kind)");

    /// <inheritdoc />
    public string Name
        => FormName;

    /// <inheritdoc />
    public LocalizationSource DisplayName
        => _displayName;

    /// <inheritdoc />
    public Task ComputeAsync(TaxReturnFormContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var taxReturn = context.Return;
        var lines = taxReturn.Lines;

        decimal Amount(string kind)
            => lines.Where(line => line.Kind == kind).Sum(line => line.Amount);

        decimal Tax(string kind)
            => lines.Where(line => line.Kind == kind).Sum(line => line.TaxAmount);

        var grossSales = Amount(TaxReturnLineKinds.GrossSales);
        var exemptSales = Amount(TaxReturnLineKinds.ExemptSales);
        var taxableSales = lines.Any(line => line.Kind == TaxReturnLineKinds.TaxableSales)
            ? Amount(TaxReturnLineKinds.TaxableSales)
            : grossSales - exemptSales;
        var salesTax = Tax(TaxReturnLineKinds.TaxableSales);
        var useTax = Tax(TaxReturnLineKinds.UseTaxPurchases);
        var withholding = Tax(TaxReturnLineKinds.Withholding);
        var excess = Tax(TaxReturnLineKinds.ExcessCollected);
        var credits = Tax(TaxReturnLineKinds.Credit);
        var totalTax = salesTax + useTax + withholding + excess;

        taxReturn.Boxes["GrossSales"] = grossSales;
        taxReturn.Boxes["ExemptSales"] = exemptSales;
        taxReturn.Boxes["TaxableSales"] = taxableSales;
        taxReturn.Boxes["SalesTax"] = salesTax;
        taxReturn.Boxes["UseTaxPurchases"] = Amount(TaxReturnLineKinds.UseTaxPurchases);
        taxReturn.Boxes["UseTax"] = useTax;
        taxReturn.Boxes["Withholding"] = withholding;
        taxReturn.Boxes["ExcessCollected"] = excess;
        taxReturn.Boxes["TotalTax"] = totalTax;
        taxReturn.Boxes["Credits"] = credits;
        taxReturn.Boxes["AmountDue"] = totalTax - credits;
        taxReturn.AmountDue = totalTax - credits;

        return Task.CompletedTask;
    }
}

/// <summary>
/// The default <see cref="ITaxReturnService"/>: periods from each registration's filing schedule, returns prepared from
/// every <see cref="ITaxReturnDataProvider"/> and worked out by the registration's <see cref="ITaxReturnForm"/>, and
/// every change told to the <see cref="ITaxReturnObserver"/>s.
/// </summary>
public sealed class TaxReturnService : ITaxReturnService
{
    private readonly ITaxReturnManager _returns;
    private readonly IMerchantTaxRegistrationStore _registrations;
    private readonly IEnumerable<ITaxReturnDataProvider> _providers;
    private readonly IEnumerable<ITaxReturnForm> _forms;
    private readonly IServiceProvider _services;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxReturnService"/> class.
    /// </summary>
    /// <param name="returns">The returns.</param>
    /// <param name="registrations">The registrations.</param>
    /// <param name="providers">The data providers.</param>
    /// <param name="forms">The forms.</param>
    /// <param name="services">The services, for the observers.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The local clock, for today's date.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxReturnService(
        ITaxReturnManager returns,
        IMerchantTaxRegistrationStore registrations,
        IEnumerable<ITaxReturnDataProvider> providers,
        IEnumerable<ITaxReturnForm> forms,
        IServiceProvider services,
        IClock clock,
        ILocalClock localClock,
        IStringLocalizer<TaxReturnService> stringLocalizer)
    {
        _returns = returns;
        _registrations = registrations;
        _providers = providers;
        _forms = forms;
        _services = services;
        _clock = clock;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ITaxReturnForm> GetForms()
    {
        var forms = new Dictionary<string, ITaxReturnForm>(StringComparer.Ordinal);

        foreach (var form in _forms)
        {
            forms[form.Name] = form;
        }

        return forms;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaxReturnPeriodSummary>> ListPeriodsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var registrations = (await _registrations.GetAllAsync(cancellationToken))
            .Where(registration => registration.IsActive && TaxFilingFrequencies.IsKnown(registration.Filing?.Frequency))
            .ToList();

        if (registrations.Count == 0)
        {
            return [];
        }

        var returns = (await _returns.ListAsync(from, to, cancellationToken))
            .Where(taxReturn => taxReturn.Status != TaxReturnStatus.Amended)
            .GroupBy(taxReturn => (taxReturn.RegistrationId, taxReturn.Form, taxReturn.PeriodKey))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(taxReturn => taxReturn.Amendment).First());
        var summaries = new List<TaxReturnPeriodSummary>();

        foreach (var registration in registrations)
        {
            var schedule = registration.Filing;

            void Add(TaxReturnPeriod period, string form, bool annual)
            {
                if (!Covers(registration, period))
                {
                    return;
                }

                summaries.Add(new TaxReturnPeriodSummary
                {
                    Registration = registration,
                    Period = period,
                    Form = form,
                    IsAnnualReconciliation = annual,
                    DueDate = schedule.DueDateOf(period, annual),
                    Return = returns.GetValueOrDefault((registration.ItemId, form, period.Key)),
                });
            }

            foreach (var period in TaxReturnPeriod.Between(schedule.Frequency, from, to))
            {
                Add(period, FormOf(schedule, annual: false), annual: false);
            }

            if (schedule.Frequency != TaxFilingFrequencies.Annual && !string.IsNullOrEmpty(schedule.AnnualForm))
            {
                foreach (var year in TaxReturnPeriod.Between(TaxFilingFrequencies.Annual, from, to))
                {
                    Add(year, schedule.AnnualForm, annual: true);
                }
            }
        }

        return [.. summaries
            .OrderBy(summary => summary.DueDate)
            .ThenBy(summary => summary.Registration.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.IsAnnualReconciliation)];
    }

    /// <inheritdoc />
    public async Task<TaxReturnResult> PrepareAsync(string registrationId, string periodKey, bool annualReconciliation, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var result = new TaxReturnResult();
        var registration = string.IsNullOrEmpty(registrationId) ? null : await _registrations.FindByIdAsync(registrationId, cancellationToken);

        if (registration is null || !TaxFilingFrequencies.IsKnown(registration.Filing?.Frequency))
        {
            result.Errors.Add(S["The registration files no return: give it a filing frequency first."]);

            return result;
        }

        var schedule = registration.Filing;
        var period = TaxReturnPeriod.Parse(periodKey);
        var frequency = annualReconciliation ? TaxFilingFrequencies.Annual : schedule.Frequency;

        if (period is null || period.Frequency != frequency || (annualReconciliation && string.IsNullOrEmpty(schedule.AnnualForm)))
        {
            result.Errors.Add(S["The registration files no return for the period {0}.", periodKey ?? string.Empty]);

            return result;
        }

        var formName = annualReconciliation ? schedule.AnnualForm : FormOf(schedule, annual: false);

        if (!GetForms().ContainsKey(formName))
        {
            result.Errors.Add(S["The return form {0} is not available: turn on the feature that adds it.", formName]);

            return result;
        }

        var current = await _returns.FindCurrentAsync(registration.ItemId, formName, period.Key, cancellationToken);

        if (current is not null)
        {
            if (current.Status == TaxReturnStatus.Draft)
            {
                return await PrepareAgainAsync(current, user, cancellationToken);
            }

            result.Return = current;
            result.Errors.Add(current.Status == TaxReturnStatus.Ready
                ? S["The return is ready to file: reopen it to prepare it again."]
                : S["The return is filed: amend it to correct it."]);

            return result;
        }

        var taxReturn = await _returns.NewAsync(cancellationToken: cancellationToken);

        taxReturn.RegistrationId = registration.ItemId;
        taxReturn.Form = formName;
        taxReturn.IsAnnualReconciliation = annualReconciliation;
        taxReturn.Frequency = period.Frequency;
        taxReturn.PeriodKey = period.Key;
        taxReturn.PeriodStart = period.Start;
        taxReturn.PeriodEnd = period.End;
        taxReturn.DueDate = schedule.DueDateOf(period, annualReconciliation);
        taxReturn.Status = TaxReturnStatus.Draft;

        await FillAsync(registration, taxReturn, user, cancellationToken);
        await _returns.CreateAsync(taxReturn, cancellationToken);
        await TellAsync(TaxReturnChangeKind.Prepared, taxReturn, null, user, cancellationToken);

        result.Return = taxReturn;

        return result;
    }

    /// <inheritdoc />
    public async Task<TaxReturnResult> PrepareAgainAsync(TaxReturn taxReturn, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxReturn);

        var result = new TaxReturnResult { Return = taxReturn };

        if (taxReturn.Status != TaxReturnStatus.Draft)
        {
            result.Errors.Add(S["Only a draft is prepared again."]);

            return result;
        }

        var registration = await _registrations.FindByIdAsync(taxReturn.RegistrationId, cancellationToken);

        if (registration is null)
        {
            result.Errors.Add(S["The registration of the return was removed."]);

            return result;
        }

        await FillAsync(registration, taxReturn, user, cancellationToken);
        await _returns.UpdateAsync(taxReturn, cancellationToken: cancellationToken);
        await TellAsync(TaxReturnChangeKind.Prepared, taxReturn, null, user, cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public Task<TaxReturnResult> MarkReadyAsync(TaxReturn taxReturn, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => MoveAsync(taxReturn, TaxReturnStatus.Draft, TaxReturnStatus.Ready, TaxReturnChangeKind.Ready, S["Only a draft is marked ready."], user, cancellationToken);

    /// <inheritdoc />
    public Task<TaxReturnResult> ReopenAsync(TaxReturn taxReturn, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => MoveAsync(taxReturn, TaxReturnStatus.Ready, TaxReturnStatus.Draft, TaxReturnChangeKind.Reopened, S["Only a return ready to file is reopened."], user, cancellationToken);

    /// <inheritdoc />
    public async Task<TaxReturnResult> MarkFiledAsync(TaxReturn taxReturn, DateOnly filedOn, string confirmationNumber, string channel, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxReturn);

        var result = new TaxReturnResult { Return = taxReturn };

        if (taxReturn.Status is not (TaxReturnStatus.Draft or TaxReturnStatus.Ready))
        {
            result.Errors.Add(S["The return is filed already."]);

            return result;
        }

        if (filedOn == default)
        {
            result.Errors.Add(S["Give the day the return was filed."]);

            return result;
        }

        // The boxes are worked out again as filed that day, from the same lines, so a late return keeps its penalty and
        // one on time keeps its discount.
        if (await _registrations.FindByIdAsync(taxReturn.RegistrationId, cancellationToken) is { } registration)
        {
            await ComputeAsync(registration, taxReturn, filedOn, cancellationToken);
        }

        taxReturn.Status = TaxReturnStatus.Filed;
        taxReturn.FiledOn = filedOn;
        taxReturn.FiledBy = ActorOf(user);
        taxReturn.ConfirmationNumber = string.IsNullOrWhiteSpace(confirmationNumber) ? null : confirmationNumber.Trim();
        taxReturn.FilingChannel = string.IsNullOrWhiteSpace(channel) ? null : channel;
        await _returns.UpdateAsync(taxReturn, cancellationToken: cancellationToken);

        // The return this one corrects is amended now.
        if (!string.IsNullOrEmpty(taxReturn.AmendsId) && await _returns.FindByIdAsync(taxReturn.AmendsId, cancellationToken) is { } amended)
        {
            amended.Status = TaxReturnStatus.Amended;
            await _returns.UpdateAsync(amended, cancellationToken: cancellationToken);
        }

        await TellAsync(TaxReturnChangeKind.Filed, taxReturn, null, user, cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public async Task<TaxReturnResult> AmendAsync(TaxReturn taxReturn, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxReturn);

        var result = new TaxReturnResult();

        if (taxReturn.Status != TaxReturnStatus.Filed)
        {
            result.Errors.Add(S["Only a filed return is amended."]);

            return result;
        }

        var current = await _returns.FindCurrentAsync(taxReturn.RegistrationId, taxReturn.Form, taxReturn.PeriodKey, cancellationToken);

        if (current is not null && current.ItemId != taxReturn.ItemId)
        {
            result.Return = current;
            result.Errors.Add(S["An amendment of the return is open already."]);

            return result;
        }

        var registration = await _registrations.FindByIdAsync(taxReturn.RegistrationId, cancellationToken);

        if (registration is null)
        {
            result.Errors.Add(S["The registration of the return was removed."]);

            return result;
        }

        var amendment = await _returns.NewAsync(cancellationToken: cancellationToken);

        amendment.RegistrationId = taxReturn.RegistrationId;
        amendment.Form = taxReturn.Form;
        amendment.IsAnnualReconciliation = taxReturn.IsAnnualReconciliation;
        amendment.Frequency = taxReturn.Frequency;
        amendment.PeriodKey = taxReturn.PeriodKey;
        amendment.PeriodStart = taxReturn.PeriodStart;
        amendment.PeriodEnd = taxReturn.PeriodEnd;
        amendment.DueDate = taxReturn.DueDate;
        amendment.Status = TaxReturnStatus.Draft;
        amendment.AmendsId = taxReturn.ItemId;
        amendment.Amendment = taxReturn.Amendment + 1;
        amendment.PaidWithEarlier = taxReturn.AmountPaid;

        await FillAsync(registration, amendment, user, cancellationToken);
        await _returns.CreateAsync(amendment, cancellationToken);
        await TellAsync(TaxReturnChangeKind.Amended, amendment, null, user, cancellationToken);

        result.Return = amendment;

        return result;
    }

    /// <inheritdoc />
    public async Task<TaxReturnResult> RecordPaymentAsync(TaxReturn taxReturn, TaxReturnPayment payment, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxReturn);
        ArgumentNullException.ThrowIfNull(payment);

        var result = new TaxReturnResult { Return = taxReturn };

        if (taxReturn.Status == TaxReturnStatus.Amended)
        {
            result.Errors.Add(S["The return was corrected by an amendment: record the payment on the amendment."]);
        }

        if (payment.Amount <= 0)
        {
            result.Errors.Add(S["The amount is more than zero."]);
        }

        if (payment.PaidOn == default)
        {
            result.Errors.Add(S["Give the day it was paid."]);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        payment.Id = IdGenerator.GenerateId();
        payment.Reference = string.IsNullOrWhiteSpace(payment.Reference) ? null : payment.Reference.Trim();
        payment.Method = string.IsNullOrWhiteSpace(payment.Method) ? null : payment.Method.Trim();
        payment.RecordedBy = ActorOf(user);
        payment.RecordedUtc = _clock.UtcNow;
        taxReturn.Payments.Add(payment);

        await _returns.UpdateAsync(taxReturn, cancellationToken: cancellationToken);
        await TellAsync(TaxReturnChangeKind.PaymentRecorded, taxReturn, payment, user, cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public async Task<TaxReturnResult> RemovePaymentAsync(TaxReturn taxReturn, string paymentId, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxReturn);

        var result = new TaxReturnResult { Return = taxReturn };
        var payment = taxReturn.Payments.FirstOrDefault(candidate => candidate.Id == paymentId);

        if (payment is null)
        {
            result.Errors.Add(S["The payment was not found."]);

            return result;
        }

        taxReturn.Payments.Remove(payment);

        await _returns.UpdateAsync(taxReturn, cancellationToken: cancellationToken);
        await TellAsync(TaxReturnChangeKind.PaymentRemoved, taxReturn, payment, user, cancellationToken);

        return result;
    }

    private static string FormOf(TaxFilingSchedule schedule, bool annual)
        => annual
            ? schedule.AnnualForm
            : string.IsNullOrEmpty(schedule.Form) ? GenericTaxReturnForm.FormName : schedule.Form;

    // A registration files for the periods it is effective in, at least in part.
    private static bool Covers(MerchantTaxRegistration registration, TaxReturnPeriod period)
    {
        if (registration.EffectiveFromUtc is { } from && DateOnly.FromDateTime(from) > period.End)
        {
            return false;
        }

        return registration.EffectiveToUtc is not { } to || DateOnly.FromDateTime(to) >= period.Start;
    }

    private static string ActorOf(ClaimsPrincipal user)
        => user?.Identity?.IsAuthenticated == true ? user.Identity.Name : null;

    private async Task<TaxReturnResult> MoveAsync(TaxReturn taxReturn, TaxReturnStatus from, TaxReturnStatus to, TaxReturnChangeKind kind, string error, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taxReturn);

        var result = new TaxReturnResult { Return = taxReturn };

        if (taxReturn.Status != from)
        {
            result.Errors.Add(error);

            return result;
        }

        taxReturn.Status = to;
        await _returns.UpdateAsync(taxReturn, cancellationToken: cancellationToken);
        await TellAsync(kind, taxReturn, null, user, cancellationToken);

        return result;
    }

    private async Task FillAsync(MerchantTaxRegistration registration, TaxReturn taxReturn, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        taxReturn.JurisdictionId = registration.JurisdictionId;
        taxReturn.TaxType = registration.TaxType;
        taxReturn.RegistrationNumber = registration.RegistrationNumber;
        taxReturn.Lines.Clear();
        taxReturn.Notes.Clear();

        var context = new TaxReturnDataContext
        {
            Registration = registration,
            Return = taxReturn,
            Period = taxReturn.Period,
        };

        foreach (var provider in _providers)
        {
            var count = taxReturn.Lines.Count;

            await provider.ContributeAsync(context, cancellationToken);

            foreach (var line in taxReturn.Lines.Skip(count))
            {
                line.Source ??= provider.Name;
            }
        }

        if (taxReturn.Lines.Count == 0)
        {
            taxReturn.Notes.Add(S["No module gave figures for the period: the return is empty."]);
        }

        // A draft past its due date is worked out as filed today, so a late return shows what it owes.
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        await ComputeAsync(registration, taxReturn, today > taxReturn.DueDate ? today : null, cancellationToken);

        taxReturn.PreparedBy = ActorOf(user);
        taxReturn.PreparedUtc = _clock.UtcNow;
    }

    private async Task ComputeAsync(MerchantTaxRegistration registration, TaxReturn taxReturn, DateOnly? filedOn, CancellationToken cancellationToken)
    {
        taxReturn.Boxes.Clear();
        taxReturn.AmountDue = 0;

        if (!GetForms().TryGetValue(taxReturn.Form ?? string.Empty, out var form))
        {
            taxReturn.Notes.Add(S["The return form {0} is not available: turn on the feature that adds it.", taxReturn.Form ?? string.Empty]);

            return;
        }

        await form.ComputeAsync(new TaxReturnFormContext
        {
            Registration = registration,
            Return = taxReturn,
            FiledOn = filedOn,
            Services = _services,
        }, cancellationToken);
    }

    private async Task TellAsync(TaxReturnChangeKind kind, TaxReturn taxReturn, TaxReturnPayment payment, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var change = new TaxReturnChange
        {
            Kind = kind,
            Return = taxReturn,
            Payment = payment,
            Actor = ActorOf(user),
        };

        // Resolved here, so an observer may use the return service itself.
        foreach (var observer in _services.GetServices<ITaxReturnObserver>())
        {
            await observer.ChangedAsync(change, cancellationToken);
        }
    }
}

/// <summary>
/// Registers Tax Returns' services.
/// </summary>
public static class TaxReturnsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the returns, their service and the generic form. The registrations they file under come from Sales Tax
    /// (<c>AddTaxationCore</c>). Calling it twice adds nothing twice.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTaxReturnsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TaxReturnStore>();
        services.TryAddScoped<ITaxReturnManager, TaxReturnManager>();
        services.TryAddScoped<ICatalogManager<TaxReturn>>(serviceProvider => serviceProvider.GetRequiredService<ITaxReturnManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TaxReturn>, TaxReturnHandler>());
        services.TryAddScoped<ITaxReturnService, TaxReturnService>();
        services.AddTaxReturnForm<GenericTaxReturnForm>();

        return services;
    }
}
