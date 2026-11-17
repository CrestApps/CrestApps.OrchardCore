using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CrestApps.Core;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Services;
using System.Security.Claims;
using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Banking.Disbursements;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
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

        order.LimitPercent ??= GarnishmentDefaults.LimitPercentFor(order);

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

        // Federal law caps support at 50% or 60% of disposable earnings, 5 points more for old arrears; an order may ask
        // for less, never more.
        if (order.Type == GarnishmentType.Support)
        {
            var maximum = GarnishmentDefaults.SupportLimitPercent(order.OtherDependents, order.ArrearsOver12Weeks);

            if (order.Percent > maximum || order.LimitPercent > maximum)
            {
                context.Result.Fail(new ValidationResult(S["A support order takes at most {0}% of disposable earnings: 50% when the person supports another spouse or child, 60% otherwise, 5 more for arrears over 12 weeks old.", maximum], [nameof(GarnishmentOrder.LimitPercent)]));
            }
        }

        // A federal levy leaves the exempt amount of its notice: without it, the levy would take all of the pay.
        if (order.Type == GarnishmentType.FederalTaxLevy && order.ExemptAmount is null)
        {
            context.Result.Fail(new ValidationResult(S["Enter the amount exempt each pay from the levy notice."], [nameof(GarnishmentOrder.ExemptAmount)]));
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

            // The payee's own account: never one of the business's, nor a staff member's or a user's personal account.
            if (account is null || account.Owner.IsEmpty || account.Owner.Kind is Staff.StaffConstants.PartyKind or PartyKinds.User)
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
            await using var locker = await LockAsync(group.OrderId);

            if (locker is null)
            {
                throw new InvalidOperationException($"The garnishment order {group.OrderId} is being worked on.");
            }

            // Read under the order's lock: another run issuing for the same order waits, then sees what this one withheld.
            if (await _orders.FindByIdAsync(group.OrderId, cancellationToken) is not { } order || order.Remittances.Any(remittance => remittance.StatementId == statement.ItemId))
            {
                continue;
            }

            // Two runs calculated against the same balance: never send the payee more than the order still owes.
            var amount = order.Remaining is { } remaining ? Math.Min(group.Amount, Math.Max(0, remaining)) : group.Amount;
            var row = new GarnishmentRemittance
            {
                StatementId = statement.ItemId,
                StatementNumber = statement.Number,
                PayDate = statement.PayDate,
                Amount = amount,
                Method = order.PaymentMethod,
            };

            if (amount < group.Amount)
            {
                row.Problem = S["{0} more was withheld than the order still owed: refund it to the employee.", (group.Amount - amount).ToString("N2", CultureInfo.CurrentCulture)];
            }

            order.Withheld += amount;

            var completed = false;

            if (order.Remaining is 0 && order.Status != GarnishmentStatus.Completed)
            {
                order.Status = GarnishmentStatus.Completed;
                completed = true;
            }

            order.Remittances.Add(row);
            await _orders.UpdateAsync(order, cancellationToken: cancellationToken);

            if (order.PaymentMethod != GarnishmentPaymentMethod.Outside && amount > 0)
            {
                await SendAsync(order, statement, row, cancellationToken);
            }

            await TellAsync(GarnishmentChangeKind.Withheld, order, statement, voided: false, cancellationToken);

            if (completed)
            {
                await TellAsync(GarnishmentChangeKind.Completed, order, statement, voided: false, cancellationToken);
            }

            await Committer().CommitAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task VoidedAsync(PayStatement statement, PayStatement reversal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        foreach (var group in Withholdings(statement))
        {
            await using var locker = await LockAsync(group.OrderId);

            if (locker is null)
            {
                throw new InvalidOperationException($"The garnishment order {group.OrderId} is being worked on.");
            }

            if (await _orders.FindByIdAsync(group.OrderId, cancellationToken) is not { } order
                || order.Remittances.FirstOrDefault(remittance => remittance.StatementId == statement.ItemId && !remittance.Voided) is not { } row)
            {
                continue;
            }

            // The withholding is given back only once the payee's payment is stopped (or none went out); a payment that
            // went out stays on the order, flagged for a person to recover.
            var stopped = string.IsNullOrEmpty(row.ReferenceId)
                || row.Method == GarnishmentPaymentMethod.Outside
                || await StopAsync(row, S["Pay statement {0} voided", statement.Number], cancellationToken);
            var fresh = await _orders.FindByIdAsync(order.ItemId, cancellationToken) ?? order;
            var freshRow = fresh.Remittances.FirstOrDefault(remittance => remittance.StatementId == statement.ItemId && !remittance.Voided) ?? row;

            if (stopped)
            {
                fresh.Withheld = Math.Max(0, fresh.Withheld - freshRow.Amount);
                freshRow.Voided = true;

                if (fresh.Status == GarnishmentStatus.Completed && fresh.Remaining is > 0)
                {
                    fresh.Status = GarnishmentStatus.Active;
                }
            }
            else
            {
                freshRow.Problem = S["The statement was voided, but the payment to the payee could not be stopped: it is in a bank file or was cashed. Ask the payee to refund it; the withholding stays on the order until then."];
            }

            await _orders.UpdateAsync(fresh, cancellationToken: cancellationToken);

            if (stopped)
            {
                await TellAsync(GarnishmentChangeKind.RemittanceVoided, fresh, statement, voided: true, cancellationToken);
            }

            await Committer().CommitAsync(cancellationToken);
        }
    }

    // Tells the observers, with the order read again: paying may have saved the remittance's payment or problem.
    private async Task TellAsync(GarnishmentChangeKind kind, GarnishmentOrder order, PayStatement statement, bool voided, CancellationToken cancellationToken)
    {
        var observers = _services.GetServices<IGarnishmentObserver>().ToArray();

        if (observers.Length == 0)
        {
            return;
        }

        var fresh = await _orders.FindByIdAsync(order.ItemId, cancellationToken) ?? order;
        var change = new GarnishmentChange
        {
            Kind = kind,
            Order = fresh,
            Remittance = fresh.Remittances.LastOrDefault(remittance => remittance.StatementId == statement.ItemId && remittance.Voided == voided),
            Statement = statement,
        };

        foreach (var observer in observers)
        {
            await observer.ChangedAsync(change, cancellationToken);
        }
    }

    /// <summary>
    /// Sends again a payment to the payee that failed: the remittance of a statement with a problem and no payment.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <param name="statementId">The statement whose remittance is sent again.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The problem, or <see langword="null"/> when the payment went out.</returns>
    public async Task<string> RetryAsync(string orderId, string statementId, CancellationToken cancellationToken = default)
    {
        await using var locker = await LockAsync(orderId);

        if (locker is null)
        {
            return S["The order is being worked on. Try again in a moment."];
        }

        if (await _orders.FindByIdAsync(orderId, cancellationToken) is not { } order
            || order.Remittances.FirstOrDefault(remittance => remittance.StatementId == statementId && !remittance.Voided) is not { } row)
        {
            return S["The payment to send again was not found."];
        }

        if (!string.IsNullOrEmpty(row.ReferenceId) || order.PaymentMethod == GarnishmentPaymentMethod.Outside || row.Amount <= 0)
        {
            return S["This payment went out already, or isn't sent from payroll."];
        }

        if (await _services.GetRequiredService<PayStatementStore>().FindByIdAsync(statementId, cancellationToken) is not { } statement
            || statement.Status is not (PayStatementStatus.Final or PayStatementStatus.Paid))
        {
            return S["The pay statement is no longer final: nothing is sent."];
        }

        row.Attempt++;
        row.Method = order.PaymentMethod;
        await _orders.UpdateAsync(order, cancellationToken: cancellationToken);

        var problem = await SendAsync(order, statement, row, cancellationToken);

        await Committer().CommitAsync(cancellationToken);

        return problem;
    }

    // Sends the remittance and records its payment or problem on a fresh read of the order.
    private async Task<string> SendAsync(GarnishmentOrder order, PayStatement statement, GarnishmentRemittance row, CancellationToken cancellationToken)
    {
        // Paying may commit the session: the order is read again before it is saved.
        var (referenceId, reference, problem) = await PayAsync(order, statement, row, cancellationToken);
        var fresh = await _orders.FindByIdAsync(order.ItemId, cancellationToken) ?? order;
        var freshRow = fresh.Remittances.LastOrDefault(remittance => remittance.StatementId == statement.ItemId && !remittance.Voided);

        if (freshRow is not null)
        {
            freshRow.ReferenceId = referenceId;
            freshRow.Reference = reference;
            freshRow.Problem = problem ?? (freshRow.Problem is { } kept && kept != row.Problem ? kept : row.Problem);
            await _orders.UpdateAsync(fresh, cancellationToken: cancellationToken);
        }

        return problem;
    }

    private async Task<ILocker> LockAsync(string orderId)
    {
        var (locker, locked) = await _services.GetRequiredService<IDistributedLock>().TryAcquireLockAsync("Payroll:Garnishment:" + orderId, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10));

        return locked ? locker : null;
    }

    private IPayrollCommitter Committer()
        => _services.GetRequiredService<IPayrollCommitter>();

    private static IEnumerable<(string OrderId, decimal Amount)> Withholdings(PayStatement statement)
        => statement.Lines
            .Where(line => line.Type == PayLineType.Deduction && !string.IsNullOrEmpty(line.GarnishmentOrderId))
            .GroupBy(line => line.GarnishmentOrderId, StringComparer.Ordinal)
            .Select(group => (group.Key, group.Sum(line => line.Amount)))
            .Where(group => group.Item2 > 0);

    private async Task<(string ReferenceId, string Reference, string Problem)> PayAsync(GarnishmentOrder order, PayStatement statement, GarnishmentRemittance row, CancellationToken cancellationToken)
    {
        var employer = await _employers.FindByIdAsync(order.EmployerId, cancellationToken);

        if (string.IsNullOrEmpty(employer?.FundingBankAccountId))
        {
            return (null, null, S["The employer has no account to pay from: choose one under its Funding account."]);
        }

        var memo = string.IsNullOrEmpty(order.CaseNumber) ? statement.Snapshot?.EmployeeName : $"{order.CaseNumber} {statement.Snapshot?.EmployeeName}";
        var key = row.Attempt > 0
            ? $"payroll-garnishment:{statement.ItemId}:{order.ItemId}:{row.Attempt}"
            : $"payroll-garnishment:{statement.ItemId}:{order.ItemId}";

        // The payment's source is the remittance, not the statement: its voucher shows the order, never the person's pay.
        var sourceId = GarnishmentSources.SourceId(order.ItemId, statement.ItemId);

        if (order.PaymentMethod == GarnishmentPaymentMethod.BankAccount)
        {
            if (_services.GetService<IDisbursementService>() is not { } disbursements || _services.GetService<IBankAccountManager>() is not { } accounts)
            {
                return (null, null, S["Turn on Disbursements to pay the payee by deposit."]);
            }

            var account = await accounts.FindByIdAsync(order.PayeeBankAccountId, cancellationToken);

            // Checked again at payment: the account may have changed hands since the order was saved.
            if (account is null || account.Owner.IsEmpty || account.Owner.Kind is Staff.StaffConstants.PartyKind or PartyKinds.User)
            {
                return (null, null, S["The payee's bank account is gone or is a personal account: choose the payee's own account on the order."]);
            }

            var created = await disbursements.CreateAsync(new DisbursementRequest
            {
                Payee = account.Owner,
                PayeeName = order.PayeeName,
                PayeeAddress = order.PayeeAddress,
                IsBusinessPayee = true,
                Amount = row.Amount,
                Currency = statement.Currency,
                Method = DisbursementsConstants.Methods.Ach,
                SourceBankAccountId = employer.FundingBankAccountId,
                DestinationBankAccountId = order.PayeeBankAccountId,
                SourceKind = GarnishmentSources.Kind,
                SourceId = sourceId,
                AccessScope = statement.EmployerId,
                Memo = memo,
                EffectiveDate = statement.PayDate,
                IdempotencyKey = key,
            }, cancellationToken);

            return created.Succeeded
                ? (created.Disbursement.ItemId, S["to {0}", account.Name].Value, null)
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
            Amount = row.Amount,
            Currency = statement.Currency,
            CheckDate = statement.PayDate,
            Memo = memo,
            SourceKind = GarnishmentSources.Kind,
            SourceId = sourceId,
            AccessScope = statement.EmployerId,
            IdempotencyKey = key,
        }, cancellationToken);

        return issued.Succeeded
            ? (issued.Check.ItemId, S["Check {0}", issued.Check.Number].Value, null)
            : (null, null, string.Join(" ", issued.Errors));
    }

    private async Task<bool> StopAsync(GarnishmentRemittance row, string reason, CancellationToken cancellationToken)
    {
        if (row.Method == GarnishmentPaymentMethod.BankAccount)
        {
            if (_services.GetService<IDisbursementManager>() is not { } manager
                || _services.GetService<IDisbursementService>() is not { } disbursements
                || await manager.FindByIdAsync(row.ReferenceId, cancellationToken) is not { } disbursement)
            {
                return false;
            }

            // A deposit the bank returned, that failed or that was cancelled never reached the payee.
            return disbursement.Status is DisbursementStatus.Returned or DisbursementStatus.Failed or DisbursementStatus.Canceled
                || await disbursements.CancelAsync(disbursement, reason, cancellationToken);
        }

        if (_services.GetService<ICheckManager>() is not { } checks
            || _services.GetService<ICheckService>() is not { } service
            || await checks.FindByIdAsync(row.ReferenceId, cancellationToken) is not { } check)
        {
            return false;
        }

        return check.Status == CheckStatus.Voided || await service.VoidAsync(check, reason, cancellationToken);
    }
}

/// <summary>
/// The source of a payment to a garnishment payee: one order's remittance of one statement, scoped by its employer.
/// </summary>
public static class GarnishmentSources
{
    /// <summary>The source kind.</summary>
    public const string Kind = "PayrollGarnishment";

    /// <summary>
    /// Gets the source id of a remittance.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <param name="statementId">The statement.</param>
    /// <returns>The source id.</returns>
    public static string SourceId(string orderId, string statementId)
        => orderId + ":" + statementId;

    /// <summary>
    /// Reads a source id back into its order and statement.
    /// </summary>
    /// <param name="sourceId">The source id.</param>
    /// <param name="orderId">The order.</param>
    /// <param name="statementId">The statement.</param>
    /// <returns>Whether the id has both.</returns>
    public static bool TryParse(string sourceId, out string orderId, out string statementId)
    {
        var parts = (sourceId ?? string.Empty).Split(':');

        (orderId, statementId) = parts.Length == 2 && parts.All(part => part.Length > 0) ? (parts[0], parts[1]) : (null, null);

        return orderId is not null;
    }
}

/// <summary>
/// Limits who sees and works the checks and deposits that pay garnishment payees to the employers they may see or
/// process the pay runs of, as for the statements they come from.
/// </summary>
public sealed class GarnishmentSourceAccess : ISourceAccess
{
    private readonly IPayrollScopeService _scope;

    /// <summary>
    /// Initializes a new instance of the <see cref="GarnishmentSourceAccess"/> class.
    /// </summary>
    /// <param name="scope">The payroll scope.</param>
    public GarnishmentSourceAccess(IPayrollScopeService scope)
    {
        _scope = scope;
    }

    /// <inheritdoc />
    public string SourceKind => GarnishmentSources.Kind;

    /// <inheritdoc />
    public async Task<SourceAccessScope> GetScopeAsync(ClaimsPrincipal user, SourceAccessLevel level, CancellationToken cancellationToken = default)
    {
        var scope = await _scope.GetScopeAsync(user, level == SourceAccessLevel.Manage ? PayrollPermissions.ProcessPayRuns : PayrollPermissions.ViewPayRuns, cancellationToken);

        return scope.All ? SourceAccessScope.All : SourceAccessScope.Of(scope.EmployerIds);
    }
}

/// <summary>
/// Prints the voucher of a check to a garnishment payee: the case, the person it is withheld from, the pay date and
/// period and the amount; never the person's earnings, taxes, other orders or year-to-date amounts.
/// </summary>
public sealed class GarnishmentCheckVoucherSource : ICheckVoucherSource
{
    private readonly IGarnishmentOrderManager _orders;
    private readonly PayStatementStore _statements;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="GarnishmentCheckVoucherSource"/> class.
    /// </summary>
    /// <param name="orders">The orders.</param>
    /// <param name="statements">The statements.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GarnishmentCheckVoucherSource(IGarnishmentOrderManager orders, PayStatementStore statements, IStringLocalizer<GarnishmentCheckVoucherSource> stringLocalizer)
    {
        _orders = orders;
        _statements = statements;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string SourceKind => GarnishmentSources.Kind;

    /// <inheritdoc />
    public async Task<CheckVoucher> GetVoucherAsync(CheckRecord check, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (!GarnishmentSources.TryParse(check.SourceId, out var orderId, out var statementId)
            || await _orders.FindByIdAsync(orderId, cancellationToken) is not { } order
            || await _statements.FindByIdAsync(statementId, cancellationToken) is not { } statement)
        {
            return null;
        }

        var amount = string.IsNullOrEmpty(check.Currency) ? check.Amount.ToString("N2", CultureInfo.CurrentCulture) : CurrencyScale.FormatForDisplay(check.Amount, check.Currency, CultureInfo.CurrentCulture);
        var lines = new List<CheckVoucherLine>();

        void Add(string label, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                lines.Add(new CheckVoucherLine { Label = label, Values = [value] });
            }
        }

        Add(S["Case number"], order.CaseNumber);
        Add(S["Court or agency"], order.Agency);
        Add(S["Withheld from"], statement.Snapshot?.EmployeeName);
        Add(S["Pay date"], statement.PayDate.ToString("d", CultureInfo.CurrentCulture));
        Add(S["Pay period"], S["{0} to {1}", statement.PeriodStart.ToString("d", CultureInfo.CurrentCulture), statement.PeriodEnd.ToString("d", CultureInfo.CurrentCulture)]);
        lines.Add(new CheckVoucherLine { Label = S["Amount"], Values = [amount], IsEmphasized = true });

        return new CheckVoucher
        {
            Title = string.IsNullOrEmpty(order.CaseNumber) ? S["Garnishment payment"] : S["Garnishment payment, case {0}", order.CaseNumber],
            Columns = [S["Remittance"], string.Empty],
            Lines = lines,
        };
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
        services.TryAddScoped<GarnishmentStatementObserver>();
        services.AddSourceAccess<GarnishmentSourceAccess>();
        services.AddCheckVoucherSource<GarnishmentCheckVoucherSource>();

        return services;
    }
}
