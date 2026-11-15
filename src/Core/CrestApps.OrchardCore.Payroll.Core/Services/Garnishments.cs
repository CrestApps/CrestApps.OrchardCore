using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CrestApps.Core;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Disbursements;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Services;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Indexes garnishment orders.
/// </summary>
public sealed class GarnishmentOrderIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the pay profile.</summary>
    public string ProfileId { get; set; }

    /// <summary>Gets or sets the staff member.</summary>
    public string StaffMemberId { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public string Status { get; set; }
}

/// <summary>
/// Maps garnishment orders to <see cref="GarnishmentOrderIndex"/> rows.
/// </summary>
public sealed class GarnishmentOrderIndexProvider : IndexProvider<GarnishmentOrder>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GarnishmentOrderIndexProvider"/> class.
    /// </summary>
    public GarnishmentOrderIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<GarnishmentOrder> context)
    {
        context
            .For<GarnishmentOrderIndex>()
            .Map(order => new GarnishmentOrderIndex
            {
                ItemId = order.ItemId,
                EmployerId = order.EmployerId,
                ProfileId = order.ProfileId,
                StaffMemberId = order.StaffMemberId,
                Status = order.Status.ToString(),
            });
    }
}

/// <summary>
/// Creates the table of garnishment orders.
/// </summary>
public static class GarnishmentOrderIndexSchema
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<GarnishmentOrderIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EmployerId", column => column.Nullable().WithLength(26))
            .Column<string>("ProfileId", column => column.Nullable().WithLength(26))
            .Column<string>("StaffMemberId", column => column.Nullable().WithLength(26))
            .Column<string>("Status", column => column.Nullable().WithLength(20)),
            collection: PayrollConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<GarnishmentOrderIndex>(table => table
            .CreateIndex("IDX_GarnishmentOrderIndex_ItemId", "ItemId", "DocumentId"),
            collection: PayrollConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<GarnishmentOrderIndex>(table => table
            .CreateIndex("IDX_GarnishmentOrderIndex_Profile", "ProfileId", "Status"),
            collection: PayrollConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<GarnishmentOrderIndex>(table => table
            .CreateIndex("IDX_GarnishmentOrderIndex_Employer", "EmployerId", "Status"),
            collection: PayrollConstants.CollectionName);
    }
}

/// <summary>
/// Stores garnishment orders in the Payroll collection.
/// </summary>
public sealed class GarnishmentOrderStore : DocumentCatalog<GarnishmentOrder, GarnishmentOrderIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GarnishmentOrderStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public GarnishmentOrderStore(ISession session)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <summary>
    /// Lists the orders matching a filter on the index.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The orders.</returns>
    public async Task<IReadOnlyList<GarnishmentOrder>> ListAsync(System.Linq.Expressions.Expression<Func<GarnishmentOrderIndex, bool>> filter, CancellationToken cancellationToken = default)
        => (await Session.Query<GarnishmentOrder, GarnishmentOrderIndex>(filter, collection: CollectionName).ListAsync(cancellationToken)).ToList();
}

/// <summary>
/// The default <see cref="IGarnishmentOrderManager"/>.
/// </summary>
public sealed class GarnishmentOrderManager : CatalogManager<GarnishmentOrder>, IGarnishmentOrderManager
{
    private readonly GarnishmentOrderStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="GarnishmentOrderManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public GarnishmentOrderManager(GarnishmentOrderStore store, IEnumerable<ICatalogEntryHandler<GarnishmentOrder>> handlers, ILogger<CatalogManager<GarnishmentOrder>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GarnishmentOrder>> ListByProfileAsync(string profileId, CancellationToken cancellationToken = default)
        => [.. (await _store.ListAsync(index => index.ProfileId == profileId, cancellationToken)).OrderBy(order => order.Priority).ThenBy(order => order.ReceivedDate)];

    /// <inheritdoc />
    public async Task<IReadOnlyList<GarnishmentOrder>> ListByEmployersAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default)
    {
        if (employerIds is null)
        {
            return [.. (await _store.GetAllAsync(cancellationToken)).OrderBy(order => order.ReceivedDate)];
        }

        if (employerIds.Count == 0)
        {
            return [];
        }

        var ids = employerIds.ToArray();

        return [.. (await _store.ListAsync(index => index.EmployerId.IsIn(ids), cancellationToken)).OrderBy(order => order.ReceivedDate)];
    }
}

/// <summary>
/// Stamps garnishment orders and checks them: a pay profile (its employer and staff member are copied), a deduction pay
/// code, an amount or a percent, limits of 0 to 100, amounts of zero or more, a payee with a way to pay it.
/// </summary>
public sealed class GarnishmentOrderHandler : CatalogEntryHandlerBase<GarnishmentOrder>
{
    private readonly IClock _clock;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="GarnishmentOrderHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="services">The services.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GarnishmentOrderHandler(IClock clock, IServiceProvider services, IStringLocalizer<GarnishmentOrderHandler> stringLocalizer)
    {
        _clock = clock;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<GarnishmentOrder> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Defaults(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<GarnishmentOrder> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task UpdatingAsync(UpdatingContext<GarnishmentOrder> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        context.Model.ModifiedUtc = _clock.UtcNow;
        await CopyProfileAsync(context.Model, cancellationToken);
    }

    /// <inheritdoc />
    public override Task CreatingAsync(CreatingContext<GarnishmentOrder> context, CancellationToken cancellationToken = default)
        => CopyProfileAsync(context.Model, cancellationToken);

    // The employer and the staff member always follow the pay profile, however the order is saved.
    private async Task CopyProfileAsync(GarnishmentOrder order, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(order.ProfileId) && await _services.GetRequiredService<IPayProfileManager>().FindByIdAsync(order.ProfileId, cancellationToken) is { } profile)
        {
            order.EmployerId = profile.EmployerId;
            order.StaffMemberId = profile.StaffMemberId;
        }
    }

    /// <summary>
    /// Fills what the kind of an order gives when an order leaves it empty: its priority, its limit and, for a creditor
    /// order, the pay a week left to the person.
    /// </summary>
    /// <param name="order">The order.</param>
    public static void Defaults(GarnishmentOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Priority == 0)
        {
            order.Priority = GarnishmentDefaults.PriorityFor(order.Type);
        }

        order.LimitPercent ??= GarnishmentDefaults.LimitPercentFor(order.Type);

        if (GarnishmentDefaults.ProtectsWeeklyAmount(order.Type))
        {
            order.ProtectedWeeklyAmount ??= GarnishmentDefaults.ProtectedWeeklyAmount;
        }
    }

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<GarnishmentOrder> context, CancellationToken cancellationToken = default)
    {
        var order = context.Model;

        if (string.IsNullOrEmpty(order.ProfileId) || await _services.GetRequiredService<IPayProfileManager>().FindByIdAsync(order.ProfileId, cancellationToken) is not { } profile)
        {
            context.Result.Fail(new ValidationResult(S["Choose the pay profile the order applies to."], [nameof(GarnishmentOrder.ProfileId)]));
        }
        else
        {
            order.EmployerId = profile.EmployerId;
            order.StaffMemberId = profile.StaffMemberId;
        }

        if (string.IsNullOrEmpty(order.PayCodeId) || await _services.GetRequiredService<IPayCodeManager>().FindByIdAsync(order.PayCodeId, cancellationToken) is not { Class: PayCodeClass.Deduction })
        {
            context.Result.Fail(new ValidationResult(S["Choose the deduction pay code the withholding is shown under."], [nameof(GarnishmentOrder.PayCodeId)]));
        }

        if (order.Amount is null && order.Percent is null)
        {
            context.Result.Fail(new ValidationResult(S["Enter an amount each pay, a percent of disposable earnings, or both."], [nameof(GarnishmentOrder.Amount)]));
        }

        if (order.Percent is < 0 or > 100 || order.LimitPercent is < 0 or > 100)
        {
            context.Result.Fail(new ValidationResult(S["A percent is from 0 to 100."], [nameof(GarnishmentOrder.Percent)]));
        }

        if (order.Amount is < 0 || order.TotalOwed is < 0 || order.ExemptAmount is < 0 || order.ProtectedWeeklyAmount is < 0)
        {
            context.Result.Fail(new ValidationResult(S["An amount is zero or more."], [nameof(GarnishmentOrder.Amount)]));
        }

        if (order.StartDate == default)
        {
            context.Result.Fail(new ValidationResult(S["Give the first pay date the order applies to."], [nameof(GarnishmentOrder.StartDate)]));
        }

        if (order.PaymentMethod == GarnishmentPaymentMethod.Check && order.PayeeAddress is null)
        {
            context.Result.Fail(new ValidationResult(S["Enter the payee's mailing address to pay by check."], [nameof(GarnishmentOrder.PaymentMethod)]));
        }

        if (order.EndDate is { } end && end < order.StartDate)
        {
            context.Result.Fail(new ValidationResult(S["The last day is before the first day."], [nameof(GarnishmentOrder.EndDate)]));
        }

        if (order.PaymentMethod != GarnishmentPaymentMethod.Outside && string.IsNullOrWhiteSpace(order.PayeeName))
        {
            context.Result.Fail(new ValidationResult(S["Enter the payee's name."], [nameof(GarnishmentOrder.PayeeName)]));
        }

        if (order.PaymentMethod == GarnishmentPaymentMethod.BankAccount)
        {
            var account = string.IsNullOrEmpty(order.PayeeBankAccountId) || _services.GetService<IBankAccountManager>() is not { } accounts
                ? null
                : await accounts.FindByIdAsync(order.PayeeBankAccountId, cancellationToken);

            // The payee's own account: never one of the business's, nor the person's.
            if (account is null || account.Owner.IsEmpty || (account.Owner.Kind == Staff.StaffConstants.PartyKind && account.Owner.Id == order.StaffMemberId))
            {
                context.Result.Fail(new ValidationResult(S["Choose the payee's bank account."], [nameof(GarnishmentOrder.PayeeBankAccountId)]));
            }
        }
    }
}

/// <summary>
/// Garnishments: each of the person's orders in effect takes what it asks, the lowest priority number first, within the
/// limit of its kind on disposable earnings (gross pay less the employee's taxes): support orders share their own
/// percent; a tax levy takes what is left above its exempt amount; a bankruptcy order takes its amount; every other
/// order shares the creditor limit, the smaller of its percent and what is left above the protected weekly amount, less
/// what the orders before it took. What a limit holds back is a warning; an order with a total owed stops at it.
/// </summary>
public sealed class GarnishmentsPayCalculationStep : IPayCalculationStep
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="GarnishmentsPayCalculationStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GarnishmentsPayCalculationStep(IStringLocalizer<GarnishmentsPayCalculationStep> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// The order of the step: after the deductions that are a percent of disposable earnings, before the net check.
    /// </summary>
    public const int StepOrder = PayCalculationOrder.Garnishments + 50;

    /// <inheritdoc />
    public int Order => StepOrder;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var orders = context.Garnishments
            .Where(order => order.AppliesOn(context.PayDate))
            .OrderBy(order => order.Priority)
            .ThenBy(order => order.ReceivedDate)
            .ThenBy(order => order.CreatedUtc)
            .ToList();

        if (orders.Count == 0)
        {
            return Task.CompletedTask;
        }

        var disposable = Math.Max(0, context.Gross - context.Lines.Where(line => line.Type == PayLineType.Tax && line.Payer == PayLinePayer.Employee).Sum(line => line.Amount));
        var weeks = 52m / PayCalculator.PeriodsPerYear(context.Schedule?.Frequency ?? PayFrequency.Biweekly);
        var supportTaken = 0m;
        var taken = 0m;

        foreach (var order in orders)
        {
            if (context.Code(order.PayCodeId) is not { } code)
            {
                context.Warnings.Add(S["The garnishment {0} names a pay code that is not there: nothing was withheld.", order.CaseNumber ?? order.ItemId]);

                continue;
            }

            var wanted = (order.Amount ?? 0) + (order.Percent is { } percent ? disposable * percent / 100m : 0);

            if (order.Remaining is { } remaining)
            {
                wanted = Math.Min(wanted, remaining);
            }

            var limit = Limit(order, disposable, weeks, supportTaken, taken);
            var amount = context.Round(Math.Max(0, Math.Min(wanted, Math.Min(limit, disposable - taken))));

            if (amount < context.Round(wanted))
            {
                context.Warnings.Add(S["The garnishment {0} asked for {1} and was limited to {2}, the most its kind may take from this pay.", order.CaseNumber ?? code.Name, context.Round(wanted).ToString("0.00", CultureInfo.InvariantCulture), amount.ToString("0.00", CultureInfo.InvariantCulture)]);
            }

            if (amount <= 0)
            {
                continue;
            }

            context.Lines.Add(new PayStatementLine
            {
                Type = PayLineType.Deduction,
                PayCodeId = code.ItemId,
                Description = string.IsNullOrEmpty(order.CaseNumber) ? code.Name : $"{code.Name} {order.CaseNumber}",
                Payer = PayLinePayer.Employee,
                Amount = amount,
                GarnishmentOrderId = order.ItemId,
            });

            taken += amount;

            if (order.Type == GarnishmentType.Support)
            {
                supportTaken += amount;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the most an order may take from a pay, before the disposable earnings left.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="disposable">The disposable earnings of the pay.</param>
    /// <param name="weeks">The weeks of the pay period.</param>
    /// <param name="supportTaken">What the support orders before it took.</param>
    /// <param name="taken">What every order before it took.</param>
    /// <returns>The limit.</returns>
    public static decimal Limit(GarnishmentOrder order, decimal disposable, decimal weeks, decimal supportTaken, decimal taken)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Type == GarnishmentType.Support)
        {
            return (disposable * (order.LimitPercent ?? 50m) / 100m) - supportTaken;
        }

        if (order.Type == GarnishmentType.Bankruptcy)
        {
            return disposable - taken;
        }

        // A levy takes what is left above its exempt amount, for a federal one always and for a state one that has one.
        if (order.Type == GarnishmentType.FederalTaxLevy || (order.Type == GarnishmentType.StateTaxLevy && order.ExemptAmount is not null))
        {
            return disposable - taken - (order.ExemptAmount ?? 0);
        }

        var limit = order.LimitPercent is { } percent ? disposable * percent / 100m : disposable;

        if (order.ProtectedWeeklyAmount is { } protectedWeekly)
        {
            limit = Math.Min(limit, disposable - (protectedWeekly * weeks));
        }

        return limit - taken;
    }
}

/// <summary>
/// Moves what garnishment orders withheld when a statement is issued or voided, and sends it to the payee by check or by
/// deposit (with Checks or Disbursements on), from the employer's funding account, as a payment of the statement (source
/// kind <c>PayStatement</c>, scoped by its employer). A void gives the withholding back and stops a payment that has not
/// gone out.
/// </summary>
public sealed class GarnishmentStatementObserver : IPayStatementObserver
{
    private readonly IGarnishmentOrderManager _orders;
    private readonly IEmployerManager _employers;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="GarnishmentStatementObserver"/> class.
    /// </summary>
    /// <param name="orders">The orders.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="services">The services, for Checks and Disbursements when they are on.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GarnishmentStatementObserver(IGarnishmentOrderManager orders, IEmployerManager employers, IServiceProvider services, IStringLocalizer<GarnishmentStatementObserver> stringLocalizer)
    {
        _orders = orders;
        _employers = employers;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task IssuedAsync(PayStatement statement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        foreach (var group in Withholdings(statement))
        {
            if (await _orders.FindByIdAsync(group.OrderId, cancellationToken) is not { } order || order.Remittances.Any(remittance => remittance.StatementId == statement.ItemId))
            {
                continue;
            }

            order.Withheld += group.Amount;

            if (order.Remaining is 0)
            {
                order.Status = GarnishmentStatus.Completed;
            }

            order.Remittances.Add(new GarnishmentRemittance
            {
                StatementId = statement.ItemId,
                StatementNumber = statement.Number,
                PayDate = statement.PayDate,
                Amount = group.Amount,
                Method = order.PaymentMethod,
            });

            await _orders.UpdateAsync(order, cancellationToken: cancellationToken);

            if (order.PaymentMethod == GarnishmentPaymentMethod.Outside)
            {
                continue;
            }

            // Paying may commit the session: the order is read again before it is saved.
            var (referenceId, reference, problem) = await PayAsync(order, statement, group.Amount, cancellationToken);
            var fresh = await _orders.FindByIdAsync(order.ItemId, cancellationToken) ?? order;
            var row = fresh.Remittances.LastOrDefault(remittance => remittance.StatementId == statement.ItemId);

            if (row is not null)
            {
                row.ReferenceId = referenceId;
                row.Reference = reference;
                row.Problem = problem;
                await _orders.UpdateAsync(fresh, cancellationToken: cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public async Task VoidedAsync(PayStatement statement, PayStatement reversal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        foreach (var group in Withholdings(statement))
        {
            if (await _orders.FindByIdAsync(group.OrderId, cancellationToken) is not { } order
                || order.Remittances.FirstOrDefault(remittance => remittance.StatementId == statement.ItemId && !remittance.Voided) is not { } row)
            {
                continue;
            }

            order.Withheld = Math.Max(0, order.Withheld - group.Amount);
            row.Voided = true;

            if (order.Status == GarnishmentStatus.Completed && order.Remaining is > 0)
            {
                order.Status = GarnishmentStatus.Active;
            }

            await _orders.UpdateAsync(order, cancellationToken: cancellationToken);

            if (string.IsNullOrEmpty(row.ReferenceId))
            {
                continue;
            }

            var stopped = await StopAsync(row, S["Pay statement {0} voided", statement.Number], cancellationToken);

            if (!stopped)
            {
                var fresh = await _orders.FindByIdAsync(order.ItemId, cancellationToken) ?? order;

                if (fresh.Remittances.FirstOrDefault(remittance => remittance.StatementId == statement.ItemId) is { } freshRow)
                {
                    freshRow.Problem = S["The payment to the payee could not be stopped: it is in a bank file or was cashed. Ask the payee to refund it."];
                    await _orders.UpdateAsync(fresh, cancellationToken: cancellationToken);
                }
            }
        }
    }

    private static IEnumerable<(string OrderId, decimal Amount)> Withholdings(PayStatement statement)
        => statement.Lines
            .Where(line => line.Type == PayLineType.Deduction && !string.IsNullOrEmpty(line.GarnishmentOrderId))
            .GroupBy(line => line.GarnishmentOrderId, StringComparer.Ordinal)
            .Select(group => (group.Key, group.Sum(line => line.Amount)))
            .Where(group => group.Item2 > 0);

    private async Task<(string ReferenceId, string Reference, string Problem)> PayAsync(GarnishmentOrder order, PayStatement statement, decimal amount, CancellationToken cancellationToken)
    {
        var employer = await _employers.FindByIdAsync(order.EmployerId, cancellationToken);

        if (string.IsNullOrEmpty(employer?.FundingBankAccountId))
        {
            return (null, null, S["The employer has no account to pay from: choose one under its Funding account."]);
        }

        var memo = string.IsNullOrEmpty(order.CaseNumber) ? statement.Snapshot?.EmployeeName : $"{order.CaseNumber} {statement.Snapshot?.EmployeeName}";

        if (order.PaymentMethod == GarnishmentPaymentMethod.BankAccount)
        {
            if (_services.GetService<IDisbursementService>() is not { } disbursements || _services.GetService<IBankAccountManager>() is not { } accounts)
            {
                return (null, null, S["Turn on Disbursements to pay the payee by deposit."]);
            }

            var account = await accounts.FindByIdAsync(order.PayeeBankAccountId, cancellationToken);
            var created = await disbursements.CreateAsync(new DisbursementRequest
            {
                Payee = account?.Owner ?? default,
                PayeeName = order.PayeeName,
                PayeeAddress = order.PayeeAddress,
                IsBusinessPayee = true,
                Amount = amount,
                Currency = statement.Currency,
                Method = DisbursementsConstants.Methods.Ach,
                SourceBankAccountId = employer.FundingBankAccountId,
                DestinationBankAccountId = order.PayeeBankAccountId,
                SourceKind = PayStatementSources.Kind,
                SourceId = statement.ItemId,
                AccessScope = statement.EmployerId,
                Memo = memo,
                EffectiveDate = statement.PayDate,
                IdempotencyKey = $"payroll-garnishment:{statement.ItemId}:{order.ItemId}",
            }, cancellationToken);

            return created.Succeeded
                ? (created.Disbursement.ItemId, account is null ? null : S["to {0}", account.Name].Value, null)
                : (null, null, string.Join(" ", created.Errors));
        }

        if (_services.GetService<ICheckService>() is not { } checks)
        {
            return (null, null, S["Turn on Checks to pay the payee by check."]);
        }

        var issued = await checks.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = employer.FundingBankAccountId,
            Payee = default,
            PayeeName = order.PayeeName,
            PayeeAddress = order.PayeeAddress,
            Amount = amount,
            Currency = statement.Currency,
            CheckDate = statement.PayDate,
            Memo = memo,
            SourceKind = PayStatementSources.Kind,
            SourceId = statement.ItemId,
            AccessScope = statement.EmployerId,
        }, cancellationToken);

        return issued.Succeeded
            ? (issued.Check.ItemId, S["Check {0}", issued.Check.Number].Value, null)
            : (null, null, string.Join(" ", issued.Errors));
    }

    private async Task<bool> StopAsync(GarnishmentRemittance row, string reason, CancellationToken cancellationToken)
    {
        if (row.Method == GarnishmentPaymentMethod.BankAccount)
        {
            return _services.GetService<IDisbursementManager>() is { } manager
                && _services.GetService<IDisbursementService>() is { } disbursements
                && await manager.FindByIdAsync(row.ReferenceId, cancellationToken) is { } disbursement
                && await disbursements.CancelAsync(disbursement, reason, cancellationToken);
        }

        return _services.GetService<ICheckManager>() is { } checks
            && _services.GetService<ICheckService>() is { } service
            && await checks.FindByIdAsync(row.ReferenceId, cancellationToken) is { } check
            && await service.VoidAsync(check, reason, cancellationToken);
    }
}

/// <summary>
/// Adds the GARN pay code (a garnishment deduction, cut last by the net check) when there is none, so an order has a code
/// to be shown under from the start.
/// </summary>
public sealed class GarnishmentPayCodeSeeder
{
    /// <summary>The code of the seeded pay code.</summary>
    public const string Code = "GARN";

    private readonly IPayCodeManager _codes;

    /// <summary>
    /// Initializes a new instance of the <see cref="GarnishmentPayCodeSeeder"/> class.
    /// </summary>
    /// <param name="codes">The pay codes.</param>
    public GarnishmentPayCodeSeeder(IPayCodeManager codes)
    {
        _codes = codes;
    }

    /// <summary>
    /// Adds the code when it is missing.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task SeedAsync()
    {
        if (await _codes.FindByCodeAsync(Code) is not null)
        {
            return;
        }

        var code = await _codes.NewAsync();

        code.Code = Code;
        code.Name = "Garnishment";
        code.Class = PayCodeClass.Deduction;
        code.Kind = PayCodeKinds.Garnishment;

        var settings = PayrollPayCodeSettingsExtensions.DefaultsFor(code);

        settings.Priority = 1;
        settings.InRetroPay = false;
        code.Put(settings);

        await _codes.CreateAsync(code);
    }
}

/// <summary>
/// Registers garnishment orders, their step in the pay calculation and the observer that sends what they withheld.
/// </summary>
public static class GarnishmentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the orders, their handler, the calculation step and the statement observer.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddGarnishmentsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<GarnishmentOrderStore>();
        services.TryAddScoped<GarnishmentPayCodeSeeder>();
        services.TryAddScoped<IGarnishmentOrderManager, GarnishmentOrderManager>();
        services.TryAddScoped<ICatalogManager<GarnishmentOrder>>(serviceProvider => serviceProvider.GetRequiredService<IGarnishmentOrderManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, GarnishmentsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayStatementObserver, GarnishmentStatementObserver>());

        return services;
    }
}
