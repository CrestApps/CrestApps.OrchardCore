using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Indexes;
using YesSql.Services;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Indexes pay runs.
/// </summary>
public sealed class PayRunIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the number.</summary>
    public string Number { get; set; }

    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the pay schedule.</summary>
    public string PayScheduleId { get; set; }

    /// <summary>Gets or sets the type.</summary>
    public string Type { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public string Status { get; set; }

    /// <summary>Gets or sets the pay date as a number, such as 20261120.</summary>
    public int PayDay { get; set; }
}

/// <summary>
/// Indexes the inputs of pay runs.
/// </summary>
public sealed class PayRunInputIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the pay run.</summary>
    public string RunId { get; set; }

    /// <summary>Gets or sets the staff member.</summary>
    public string StaffMemberId { get; set; }

    /// <summary>Gets or sets the pay code.</summary>
    public string PayCodeId { get; set; }
}

/// <summary>
/// Maps pay runs to <see cref="PayRunIndex"/> rows.
/// </summary>
public sealed class PayRunIndexProvider : IndexProvider<PayRun>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunIndexProvider"/> class.
    /// </summary>
    public PayRunIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayRun> context)
        => context.For<PayRunIndex>().Map(run => new PayRunIndex
        {
            ItemId = run.ItemId,
            Number = run.Number,
            EmployerId = run.EmployerId,
            PayScheduleId = run.PayScheduleId,
            Type = run.Type.ToString(),
            Status = run.Status.ToString(),
            PayDay = PayStatementLineIndex.DayNumber(run.PayDate),
        });
}

/// <summary>
/// Maps pay run inputs to <see cref="PayRunInputIndex"/> rows.
/// </summary>
public sealed class PayRunInputIndexProvider : IndexProvider<PayRunInput>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunInputIndexProvider"/> class.
    /// </summary>
    public PayRunInputIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayRunInput> context)
        => context.For<PayRunInputIndex>().Map(input => new PayRunInputIndex
        {
            ItemId = input.ItemId,
            RunId = input.RunId,
            StaffMemberId = input.StaffMemberId,
            PayCodeId = input.PayCodeId,
        });
}

/// <summary>
/// Creates the tables of pay runs and their inputs.
/// </summary>
public static class PayRunIndexSchema
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = PayrollConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<PayRunIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Number", column => column.Nullable().WithLength(40))
            .Column<string>("EmployerId", column => column.Nullable().WithLength(26))
            .Column<string>("PayScheduleId", column => column.Nullable().WithLength(26))
            .Column<string>("Type", column => column.Nullable().WithLength(20))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<int>("PayDay"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayRunIndex>(table =>
        {
            table.CreateIndex("IDX_PayRunIndex_ItemId", "ItemId", "DocumentId");
            table.CreateIndex("IDX_PayRunIndex_Employer", "EmployerId", "PayDay", "DocumentId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<PayRunInputIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("RunId", column => column.Nullable().WithLength(26))
            .Column<string>("StaffMemberId", column => column.Nullable().WithLength(26))
            .Column<string>("PayCodeId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayRunInputIndex>(table =>
        {
            table.CreateIndex("IDX_PayRunInputIndex_ItemId", "ItemId", "DocumentId");
            table.CreateIndex("IDX_PayRunInputIndex_Run", "RunId", "DocumentId");
            table.CreateIndex("IDX_PayRunInputIndex_PayCode", "PayCodeId", "DocumentId");
        }, collection: collection);
    }
}

/// <summary>
/// Stores pay runs. A run that was processed is never deleted; an open run is cancelled, not deleted.
/// </summary>
public sealed class PayRunStore : DocumentCatalog<PayRun, PayRunIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public PayRunStore(ISession session)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    // Two people working on the same run must not both write a stale copy.
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Lists the runs matching a filter on the index.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The runs.</returns>
    public async Task<IReadOnlyList<PayRun>> ListAsync(System.Linq.Expressions.Expression<Func<PayRunIndex, bool>> filter, CancellationToken cancellationToken = default)
        => (await Session.Query<PayRun, PayRunIndex>(filter, collection: CollectionName).ListAsync(cancellationToken)).ToList();

    /// <inheritdoc />
    protected override ValueTask DeletingAsync(PayRun entry)
    {
        if (entry.Status is not (PayRunStatus.Draft or PayRunStatus.Canceled))
        {
            throw new InvalidOperationException("A pay run that was calculated is cancelled, not deleted.");
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Stores the inputs of pay runs.
/// </summary>
public sealed class PayRunInputStore : DocumentCatalog<PayRunInput, PayRunInputIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunInputStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public PayRunInputStore(ISession session)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <summary>
    /// Lists the inputs matching a filter on the index.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The inputs.</returns>
    public async Task<IReadOnlyList<PayRunInput>> ListAsync(System.Linq.Expressions.Expression<Func<PayRunInputIndex, bool>> filter, CancellationToken cancellationToken = default)
        => (await Session.Query<PayRunInput, PayRunInputIndex>(filter, collection: CollectionName).ListAsync(cancellationToken)).ToList();

    /// <summary>
    /// Counts the inputs matching a filter on the index.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count.</returns>
    public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<PayRunInputIndex, bool>> filter, CancellationToken cancellationToken = default)
        => Session.QueryIndex<PayRunInputIndex>(filter, collection: CollectionName).CountAsync(cancellationToken);
}

/// <summary>
/// The default <see cref="IPayRunManager"/>.
/// </summary>
public sealed class PayRunManager : CatalogManager<PayRun>, IPayRunManager
{
    private readonly PayRunStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public PayRunManager(PayRunStore store, IEnumerable<ICatalogEntryHandler<PayRun>> handlers, ILogger<CatalogManager<PayRun>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PayRun>> ListByEmployersAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employerIds);

        if (employerIds.Count == 0)
        {
            return [];
        }

        var ids = employerIds.ToArray();

        return [.. (await _store.ListAsync(index => index.EmployerId.IsIn(ids), cancellationToken)).OrderByDescending(run => run.PayDate).ThenByDescending(run => run.CreatedUtc)];
    }
}

/// <summary>
/// The default <see cref="IPayRunInputManager"/>.
/// </summary>
public sealed class PayRunInputManager : CatalogManager<PayRunInput>, IPayRunInputManager
{
    private readonly PayRunInputStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunInputManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public PayRunInputManager(PayRunInputStore store, IEnumerable<ICatalogEntryHandler<PayRunInput>> handlers, ILogger<CatalogManager<PayRunInput>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PayRunInput>> ListByRunAsync(string runId, CancellationToken cancellationToken = default)
        => _store.ListAsync(index => index.RunId == runId, cancellationToken);
}

/// <summary>
/// Numbers, stamps and checks pay runs.
/// </summary>
public sealed class PayRunHandler : CatalogEntryHandlerBase<PayRun>
{
    private readonly INumberSequence _numbers;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunHandler"/> class.
    /// </summary>
    /// <param name="numbers">The number sequences.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayRunHandler(INumberSequence numbers, IClock clock, IStringLocalizer<PayRunHandler> stringLocalizer)
    {
        _numbers = numbers;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<PayRun> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task CreatingAsync(CreatingContext<PayRun> context, CancellationToken cancellationToken = default)
    {
        // The number is given when the run is saved, not when the form is shown.
        if (string.IsNullOrEmpty(context.Model.Number))
        {
            context.Model.Number = (await _numbers.NextAsync(PayrollConstants.PayRunNumberSeries, cancellationToken: cancellationToken)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<PayRun> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidatingAsync(ValidatingContext<PayRun> context, CancellationToken cancellationToken = default)
    {
        var run = context.Model;

        if (string.IsNullOrEmpty(run.EmployerId))
        {
            context.Result.Fail(new ValidationResult(S["Choose the employer."], [nameof(PayRun.EmployerId)]));
        }

        if (string.IsNullOrEmpty(run.PayScheduleId) && run.StaffMemberIds.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["Choose the pay schedule whose people are paid, or the people."], [nameof(PayRun.PayScheduleId)]));
        }

        if (run.PeriodStart == default || run.PeriodEnd < run.PeriodStart)
        {
            context.Result.Fail(new ValidationResult(S["The period ends on or after the day it starts."], [nameof(PayRun.PeriodEnd)]));
        }

        if (run.PayDate == default)
        {
            context.Result.Fail(new ValidationResult(S["Give the pay date."], [nameof(PayRun.PayDate)]));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Stamps and checks the inputs of pay runs: a staff member, a pay code, hours or an amount of zero or more.
/// </summary>
public sealed class PayRunInputHandler : CatalogEntryHandlerBase<PayRunInput>
{
    private readonly IClock _clock;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunInputHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for who entered an input on a page.</param>
    public PayRunInputHandler(IClock clock, IStringLocalizer<PayRunInputHandler> stringLocalizer, Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor = null)
    {
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<PayRunInput> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;
        context.Model.EnteredById ??= _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<PayRunInput> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidatingAsync(ValidatingContext<PayRunInput> context, CancellationToken cancellationToken = default)
    {
        var input = context.Model;

        if (string.IsNullOrEmpty(input.RunId) || string.IsNullOrEmpty(input.StaffMemberId) || string.IsNullOrEmpty(input.PayCodeId))
        {
            context.Result.Fail(new ValidationResult(S["An input needs its run, its person and its pay code."], [nameof(PayRunInput.PayCodeId)]));
        }

        if (input.Hours is null && input.Amount is null)
        {
            context.Result.Fail(new ValidationResult(S["Give hours or an amount."], [nameof(PayRunInput.Amount)]));
        }

        if (input.Hours is < 0 || input.Rate is < 0)
        {
            context.Result.Fail(new ValidationResult(S["Hours and rates are zero or more."], [nameof(PayRunInput.Hours)]));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// The default <see cref="IPayRunService"/>. A run is calculated in the request, one profile at a time, under a lock
/// on the run, so two calculations of one run never interleave.
/// </summary>
public sealed partial class DefaultPayRunService : IPayRunService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(10);

    private readonly IPayRunManager _runs;
    private readonly IPayRunInputManager _inputs;
    private readonly IPayProfileManager _profiles;
    private readonly IPayProfileService _profileService;
    private readonly PayStatementStore _statements;
    private readonly PayCalculationContextFactory _contexts;
    private readonly IPayCalculator _calculator;
    private readonly IEmployerManager _employers;
    private readonly IStaffDirectory _staff;
    private readonly IDistributedLock _locks;
    private readonly IClock _clock;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPayRunService"/> class.
    /// </summary>
    /// <param name="runs">The runs.</param>
    /// <param name="inputs">The run inputs.</param>
    /// <param name="profiles">The pay profiles.</param>
    /// <param name="profileService">The profile service, for blockers.</param>
    /// <param name="statements">The statements.</param>
    /// <param name="contexts">Builds calculations.</param>
    /// <param name="calculator">The calculator.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="staff">The staff directory.</param>
    /// <param name="locks">The distributed lock.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="services">The services, for Approvals and the settings.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultPayRunService(
        IPayRunManager runs,
        IPayRunInputManager inputs,
        IPayProfileManager profiles,
        IPayProfileService profileService,
        PayStatementStore statements,
        PayCalculationContextFactory contexts,
        IPayCalculator calculator,
        IEmployerManager employers,
        IStaffDirectory staff,
        IDistributedLock locks,
        IClock clock,
        IServiceProvider services,
        IStringLocalizer<DefaultPayRunService> stringLocalizer)
    {
        _runs = runs;
        _inputs = inputs;
        _profiles = profiles;
        _profileService = profileService;
        _statements = statements;
        _contexts = contexts;
        _calculator = calculator;
        _employers = employers;
        _staff = staff;
        _locks = locks;
        _clock = clock;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<PayRunResult> CalculateAsync(PayRun run, ClaimsPrincipal user, IReadOnlyCollection<string> staffMemberIds = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var result = new PayRunResult();

        if (!run.IsOpen)
        {
            result.Errors.Add(S["A pay run that was processed or cancelled can't be calculated again."]);

            return result;
        }

        if (await YearClosedAsync(run.EmployerId, run.PayDate, cancellationToken))
        {
            result.Errors.Add(S["The employer's year {0} is closed: reopen it at year end first.", run.PayDate.Year]);

            return result;
        }

        var (locker, locked) = await _locks.TryAcquireLockAsync("Payroll:PayRun:" + run.ItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Errors.Add(S["The pay run is being calculated. Try again in a moment."]);

            return result;
        }

        await using (locker)
        {
            // A caller that waited for the lock decides on the run as it is stored now, not the copy it read before.
            run = await _runs.FindByIdAsync(run.ItemId, cancellationToken) ?? run;

            if (!run.IsOpen || (await StoredRunAsync(run.ItemId, cancellationToken)) is { IsOpen: false })
            {
                result.Errors.Add(S["A pay run that was processed or cancelled can't be calculated again."]);

                return result;
            }

            // An approval is for one calculation: a run waiting for one, or approved, goes back to calculated.
            if (run.Status is PayRunStatus.PendingApproval or PayRunStatus.Approved)
            {
                run = await CancelApprovalAsync(run, S["The pay run was calculated again."], cancellationToken);
            }

            var profiles = await ProfilesAsync(run, cancellationToken);
            var only = staffMemberIds?.ToHashSet(StringComparer.Ordinal);
            var inputs = (await _inputs.ListByRunAsync(run.ItemId, cancellationToken)).ToLookup(input => input.StaffMemberId, StringComparer.Ordinal);
            var drafts = (await ListStatementsAsync(run, cancellationToken)).Where(statement => statement.Status == PayStatementStatus.Draft).ToList();
            var employer = await _employers.FindByIdAsync(run.EmployerId, cancellationToken);
            var names = await _staff.GetAsync(profiles.Select(profile => profile.StaffMemberId), cancellationToken);
            var issues = run.Issues.Where(issue => only is not null && issue.StaffMemberId is not null && !only.Contains(issue.StaffMemberId)).ToList();

            foreach (var profile in profiles.Where(profile => only is null || only.Contains(profile.StaffMemberId)))
            {
                var context = await CalculateOneAsync(run, profile, [.. inputs[profile.StaffMemberId]], cancellationToken);

                var statement = drafts.FirstOrDefault(draft => draft.ProfileId == profile.ItemId) ?? new PayStatement { ItemId = UniqueId.GenerateId(), CreatedUtc = _clock.UtcNow };
                var staffMember = names.GetValueOrDefault(profile.StaffMemberId);

                statement.RunId = run.ItemId;
                statement.ProfileId = profile.ItemId;
                statement.StaffMemberId = profile.StaffMemberId;
                statement.EmployerId = run.EmployerId;
                statement.PayDate = run.PayDate;
                statement.PeriodStart = run.PeriodStart;
                statement.PeriodEnd = run.PeriodEnd;
                statement.Currency = context.Currency;
                statement.Lines = [.. context.Lines];
                statement.Payments = [.. context.Payments];
                statement.Warnings = [.. context.Warnings];
                statement.Gross = context.Gross;
                statement.Net = context.Round(context.Net);
                statement.Status = PayStatementStatus.Draft;
                statement.ModifiedUtc = _clock.UtcNow;
                statement.Snapshot = new PayStatementSnapshot
                {
                    EmployeeName = string.IsNullOrEmpty(profile.LegalName) ? staffMember?.DisplayName : profile.LegalName,
                    EmployeeNumber = staffMember?.EmployeeNumber,
                    TaxIdHint = profile.TaxId?.Hint,
                    EmployerName = employer?.PrintedName,
                };
                statement.Balances = [.. await BalancesAsync(profile.StaffMemberId, run.PayDate, cancellationToken)];

                if (drafts.Contains(statement))
                {
                    await _statements.UpdateAsync(statement, cancellationToken);
                }
                else
                {
                    await _statements.CreateAsync(statement, cancellationToken);
                    drafts.Add(statement);
                }

                foreach (var blocker in await _profileService.GetBlockersAsync(profile, cancellationToken))
                {
                    issues.Add(new PayRunIssue { StaffMemberId = profile.StaffMemberId, Message = blocker.Message, IsBlocker = true });
                }

                issues.AddRange(context.Warnings.Select(warning => new PayRunIssue { StaffMemberId = profile.StaffMemberId, Message = warning }));
            }

            // A full calculation drops the drafts of people no longer in the run.
            if (only is null)
            {
                var kept = profiles.Select(profile => profile.ItemId).ToHashSet(StringComparer.Ordinal);

                foreach (var stale in drafts.Where(draft => !kept.Contains(draft.ProfileId)).ToList())
                {
                    await _statements.DeleteAsync(stale, cancellationToken);
                    drafts.Remove(stale);
                }
            }

            if (profiles.Count == 0)
            {
                issues.Add(new PayRunIssue { Message = S["No one is paid by this run: no active pay profile of the employer is on the pay schedule."], IsBlocker = true });
            }

            run.Totals = Totals(drafts);
            run.Issues = issues;
            run.Version = UniqueId.GenerateId();
            run.Status = PayRunStatus.Calculated;
            run.CalculatedUtc = _clock.UtcNow;
            run.PreparedById = user?.FindFirstValue(ClaimTypes.NameIdentifier);
            run.PreparedBy = user?.Identity?.Name;
            await _runs.UpdateAsync(run, cancellationToken: cancellationToken);
            await TellAsync(run, PayRunChangeKind.Calculated, user, null, cancellationToken);
            await Committer().CommitAsync(cancellationToken);
        }

        return result;
    }

    // Commits while the run's lock is held.
    private IPayrollCommitter Committer()
        => _services.GetRequiredService<IPayrollCommitter>();

    // The balances a statement shows, such as time off, from every source.
    private async Task<IReadOnlyList<PayStatementBalance>> BalancesAsync(string staffMemberId, DateOnly payDate, CancellationToken cancellationToken)
    {
        var balances = new List<PayStatementBalance>();

        foreach (var source in _services.GetServices<IPayStatementBalanceSource>())
        {
            balances.AddRange(await source.GetBalancesAsync(staffMemberId, payDate, cancellationToken));
        }

        return balances;
    }

    /// <inheritdoc />
    public async Task<PayRunResult> OpenAsync(PayRun run, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var result = new PayRunResult();

        if (await YearClosedAsync(run.EmployerId, run.PayDate, cancellationToken))
        {
            result.Errors.Add(S["The employer's year {0} is closed: reopen it at year end first.", run.PayDate.Year]);

            return result;
        }
        var validation = await _runs.ValidateAsync(run, cancellationToken);

        if (!validation.Succeeded)
        {
            foreach (var error in validation.Errors)
            {
                result.Errors.Add(error.ErrorMessage);
            }

            return result;
        }

        await _runs.CreateAsync(run, cancellationToken);
        await TellAsync(run, PayRunChangeKind.Opened, user, null, cancellationToken);

        return result;
    }

    // The users who prepared a run: who calculated it, who entered its inputs, and who added or changed, since the start
    // of its period, a pay item of a person it pays.
    private async Task<List<string>> SubjectsAsync(PayRun run, CancellationToken cancellationToken)
    {
        var subjects = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(run.PreparedById))
        {
            subjects.Add(run.PreparedById);
        }

        foreach (var input in await _inputs.ListByRunAsync(run.ItemId, cancellationToken))
        {
            if (!string.IsNullOrEmpty(input.EnteredById))
            {
                subjects.Add(input.EnteredById);
            }
        }

        var since = run.PeriodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);
        var items = _services.GetRequiredService<IPayItemManager>();

        foreach (var profileId in (await ListStatementsAsync(run, cancellationToken)).Select(statement => statement.ProfileId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            foreach (var item in await items.ListByProfileAsync(profileId, cancellationToken))
            {
                if (!string.IsNullOrEmpty(item.ChangedById) && (item.ModifiedUtc ?? item.CreatedUtc) >= since)
                {
                    subjects.Add(item.ChangedById);
                }
            }
        }

        return [.. subjects];
    }

    /// <inheritdoc />
    public async Task<PayRunResult> SubmitAsync(PayRun run, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var result = new PayRunResult();

        if (run.Status != PayRunStatus.Calculated)
        {
            result.Errors.Add(S["Calculate the pay run before submitting it."]);

            return result;
        }

        if (run.Issues.Any(issue => issue.IsBlocker))
        {
            result.Errors.Add(S["The pay run has blockers: fix them and calculate it again."]);

            return result;
        }

        if (_services.GetService<IApprovalService>() is not { } approvals)
        {
            result.Errors.Add(S["Approvals is not turned on."]);

            return result;
        }

        var settings = _services.GetService<ISiteService>() is { } site ? await site.GetSettingsAsync<PayrollSettings>() : null;
        var employer = await _employers.FindByIdAsync(run.EmployerId, cancellationToken);
        var subjects = settings?.AllowPreparerToApprove == true ? new List<string>() : await SubjectsAsync(run, cancellationToken);
        var submitted = await approvals.RequestAsync(new ApprovalSubmission
        {
            Policy = PayrollConstants.PayRunApprovalPolicy,
            SubjectKind = PayrollConstants.PayRunSubjectKind,
            SubjectId = run.ItemId,
            AccessScope = run.EmployerId,
            SubjectUrl = $"~/Admin/payroll/pay-runs/{run.ItemId}",
            Title = S["Pay run {0} of {1}, paid {2}", run.Number, employer?.Name ?? run.EmployerId, run.PayDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)],
            Amount = run.Totals.CashNeeded,
            Currency = employer?.Currency,
            SubjectVersion = run.Version,

            // By default nobody who prepared the run may approve it: who calculated it, who entered its inputs and who
            // changed the pay items it pays, even when a timer calculated it.
            SubjectUserIds = subjects,
        }, user, cancellationToken);

        if (!submitted.Succeeded)
        {
            foreach (var error in submitted.Errors)
            {
                result.Errors.Add(error);
            }

            return result;
        }

        // Approvals commits the request, so the session forgot what it read before: read the run and the request again.
        run = await _runs.FindByIdAsync(run.ItemId, cancellationToken) ?? run;

        // The policy names who approves for every employer; the approvers of this employer only may decide too.
        if (submitted.Request.Status == ApprovalStatus.Pending && _services.GetService<IApprovalRequestManager>() is { } requests
            && await requests.FindByIdAsync(submitted.Request.ItemId, cancellationToken) is { } request)
        {
            var perEmployer = PayrollPermissionTemplates.For(PayrollPermissions.ApprovePayRuns, run.EmployerId).Name;

            if (!request.Approvers.Any(rule => rule.Kind == ApproverKind.Permission && rule.Value == perEmployer))
            {
                request.Approvers.Add(new ApproverRule { Kind = ApproverKind.Permission, Value = perEmployer });
                await requests.UpdateAsync(request, cancellationToken: cancellationToken);
            }
        }

        run.ApprovalRequestId = submitted.Request.ItemId;
        run.Status = submitted.Request.Status == ApprovalStatus.Approved ? PayRunStatus.Approved : PayRunStatus.PendingApproval;

        if (run.Status == PayRunStatus.Approved)
        {
            run.ApprovedUtc = _clock.UtcNow;
        }

        await _runs.UpdateAsync(run, cancellationToken: cancellationToken);
        await TellAsync(run, PayRunChangeKind.Submitted, user, null, cancellationToken);

        if (run.Status == PayRunStatus.Approved)
        {
            await TellAsync(run, PayRunChangeKind.Approved, user, null, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PayRunResult> CancelAsync(PayRun run, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var result = new PayRunResult();

        var (locker, locked) = await _locks.TryAcquireLockAsync("Payroll:PayRun:" + run.ItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Errors.Add(S["The pay run is being worked on. Try again in a moment."]);

            return result;
        }

        await using (locker)
        {
            run = await _runs.FindByIdAsync(run.ItemId, cancellationToken) ?? run;

            if (!PayRunStatuses.Transitions.CanMove(run.Status, PayRunStatus.Canceled)
                || (await StoredRunAsync(run.ItemId, cancellationToken)) is { } stored && !PayRunStatuses.Transitions.CanMove(stored.Status, PayRunStatus.Canceled))
            {
                result.Errors.Add(S["A pay run that was processed can't be cancelled: void its statements instead."]);

                return result;
            }

            run = await CancelApprovalAsync(run, reason, cancellationToken);

            foreach (var draft in (await ListStatementsAsync(run, cancellationToken)).Where(statement => statement.Status == PayStatementStatus.Draft))
            {
                await _statements.DeleteAsync(draft, cancellationToken);
            }

            run.Status = PayRunStatus.Canceled;
            run.Totals = new PayRunTotals();
            await _runs.UpdateAsync(run, cancellationToken: cancellationToken);
            await TellAsync(run, PayRunChangeKind.Canceled, null, reason, cancellationToken);
            await Committer().CommitAsync(cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PayStatement>> ListStatementsAsync(PayRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var id = run.ItemId;

        return _statements.ListAsync(index => index.RunId == id, cancellationToken);
    }

    /// <summary>
    /// Takes an approval's decision: approved for the run's current calculation moves it to approved; rejected or
    /// cancelled returns it to calculated.
    /// </summary>
    /// <param name="request">The closed request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task ApprovalClosedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _runs.FindByIdAsync(request.SubjectId, cancellationToken) is not { } run || run.ApprovalRequestId != request.ItemId)
        {
            return;
        }

        var decision = request.Decisions.LastOrDefault();
        PayRunChangeKind? kind = null;

        if (request.Status == ApprovalStatus.Approved && request.SubjectVersion == run.Version && run.Status == PayRunStatus.PendingApproval)
        {
            run.Status = PayRunStatus.Approved;
            run.ApprovedUtc = _clock.UtcNow;
            kind = PayRunChangeKind.Approved;
        }
        else if (run.Status is PayRunStatus.PendingApproval or PayRunStatus.Approved)
        {
            run.Status = PayRunStatus.Calculated;
            run.ApprovalRequestId = null;

            if (request.Status == ApprovalStatus.Rejected)
            {
                run.Issues.Add(new PayRunIssue { Message = S["The approval was rejected: {0}", decision?.Note ?? string.Empty] });
                kind = PayRunChangeKind.Rejected;
            }
        }
        else
        {
            return;
        }

        await _runs.UpdateAsync(run, cancellationToken: cancellationToken);

        if (kind is { } happened)
        {
            await TellAsync(run, happened, decision?.UserName, decision?.UserId, decision?.Note, cancellationToken);
        }
    }

    private Task TellAsync(PayRun run, PayRunChangeKind kind, ClaimsPrincipal user, string note, CancellationToken cancellationToken)
        => TellAsync(run, kind, user?.Identity?.Name, user?.FindFirstValue(ClaimTypes.NameIdentifier), note, cancellationToken);

    // Tells the observers (workflows, webhooks, the audit trail) what happened to a run.
    private async Task TellAsync(PayRun run, PayRunChangeKind kind, string actor, string actorId, string note, CancellationToken cancellationToken)
    {
        var change = new PayRunChange { Run = run, Kind = kind, Actor = actor, ActorId = actorId, Note = note };

        foreach (var observer in _services.GetServices<IPayRunObserver>())
        {
            await observer.ChangedAsync(change, cancellationToken);
        }
    }

    // The profiles a run pays: the people named, or every active profile of the employer on the schedule.
    private async Task<IReadOnlyList<EmployeePayProfile>> ProfilesAsync(PayRun run, CancellationToken cancellationToken)
    {
        var profiles = await _profiles.ListByEmployersAsync([run.EmployerId], cancellationToken);

        return run.StaffMemberIds.Count > 0
            ? [.. profiles.Where(profile => run.StaffMemberIds.Contains(profile.StaffMemberId))]
            : [.. profiles.Where(profile => profile.PayScheduleId == run.PayScheduleId && profile.Status == PayProfileStatus.Active
                && (run.Type != PayRunType.Contractor || profile.WorkerType == PayWorkerType.Contractor))];
    }

    // Cancels the run's pending approval. Approvals commits the cancellation, so the run is read again: the caller goes
    // on with the run this returns.
    private async Task<PayRun> CancelApprovalAsync(PayRun run, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(run.ApprovalRequestId))
        {
            return run;
        }

        if (_services.GetService<IApprovalRequestManager>() is { } requests && await requests.FindByIdAsync(run.ApprovalRequestId, cancellationToken) is { Status: ApprovalStatus.Pending } request
            && _services.GetService<IApprovalService>() is { } approvals)
        {
            await approvals.CancelAsync(request, reason, cancellationToken);
            run = await _runs.FindByIdAsync(run.ItemId, cancellationToken) ?? run;
        }

        run.ApprovalRequestId = null;
        run.ApprovedUtc = null;
        run.Status = PayRunStatus.Calculated;

        return run;
    }

    // Calculates one person's pay. An amount entered as what the person takes home is grossed up: the pay is worked out
    // without it, then again with the earning raised until net pay is that much more.
    private async Task<PayCalculationContext> CalculateOneAsync(PayRun run, EmployeePayProfile profile, IReadOnlyList<PayRunInput> inputs, CancellationToken cancellationToken)
    {
        var nets = inputs.Where(input => input.IsNet && input.Amount is > 0).ToList();
        var net = nets.FirstOrDefault();
        var others = inputs.Where(input => !input.IsNet).ToList();

        PayCalculationRequest Request(IEnumerable<PayRunInput> extra)
            => new()
            {
                PeriodStart = run.PeriodStart,
                PeriodEnd = run.PeriodEnd,
                PayDate = run.PayDate,
                Regular = run.IsRegular,
                IncludeSourceInputs = run.IsRegular,
                SupplementalMethod = run.SupplementalMethod,
                ExtraInputs = [.. extra.Select(input => ToPayInput(run, input))],
            };

        var context = await _contexts.CreateAsync(profile, Request(others), cancellationToken);

        await _calculator.CalculateAsync(context, cancellationToken);

        if (net is null)
        {
            return context;
        }

        var grossed = await _contexts.CreateAsync(profile, Request(others), cancellationToken);

        // The gross-up pays the taxes; garnishments are taken from the pay as from any other.
        var garnished = context.Lines.Where(line => !string.IsNullOrEmpty(line.GarnishmentOrderId)).Sum(line => line.Amount);

        grossed.GrossUp = new PayGrossUp { PayCodeId = net.PayCodeId, TargetNet = context.Round(context.Net + garnished) + net.Amount.Value, BeforeGarnishments = true };
        await _calculator.CalculateAsync(grossed, cancellationToken);

        foreach (var extra in nets.Skip(1))
        {
            grossed.Warnings.Add(S["Only one amount a pay can be grossed up: {0} was left out.", extra.Amount?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)]);
        }

        return grossed;
    }

    /// <summary>
    /// Gets whether an employer's year of a pay date was closed at year end.
    /// </summary>
    /// <param name="employerId">The employer.</param>
    /// <param name="payDate">The pay date.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it is closed.</returns>
    internal async Task<bool> YearClosedAsync(string employerId, DateOnly payDate, CancellationToken cancellationToken)
        => !string.IsNullOrEmpty(employerId) && await _employers.FindByIdAsync(employerId, cancellationToken) is { } employer && employer.ClosedYears.Contains(payDate.Year);

    private static PayInput ToPayInput(PayRun run, PayRunInput input)
        => new()
        {
            SourceKind = PayrollConstants.PayRunSubjectKind,
            SourceId = input.ItemId,
            StaffMemberId = input.StaffMemberId,
            PayCodeId = input.PayCodeId,
            Date = input.Date,
            Hours = input.Hours,
            Rate = input.Rate,
            Amount = input.Amount,
            EarnedFrom = input.EarnedFrom,
            EarnedTo = input.EarnedTo,
            PeriodStart = run.PeriodStart,
            PeriodEnd = run.PeriodEnd,
        };

    internal static PayRunTotals Totals(IEnumerable<PayStatement> statements)
    {
        var totals = new PayRunTotals();

        foreach (var statement in statements)
        {
            totals.Statements++;
            totals.Gross += statement.Gross;
            totals.Net += statement.Net;

            foreach (var line in statement.Lines)
            {
                switch (line.Type)
                {
                    case PayLineType.Tax when line.Payer == PayLinePayer.Employer:
                        totals.EmployerTaxes += line.Amount;
                        break;
                    case PayLineType.Tax:
                        totals.EmployeeTaxes += line.Amount;
                        break;
                    case PayLineType.Deduction:
                        totals.Deductions += line.Amount;
                        break;
                    case PayLineType.EmployerContribution:
                        totals.EmployerContributions += line.Amount;
                        break;
                    case PayLineType.Reimbursement:
                        totals.Reimbursements += line.Amount;
                        break;
                }
            }
        }

        return totals;
    }
}

/// <summary>
/// Takes Approvals' decision on a pay run.
/// </summary>
public sealed class PayRunApprovalSubjectHandler : IApprovalSubjectHandler
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunApprovalSubjectHandler"/> class.
    /// </summary>
    /// <param name="services">The services, to reach the pay run service when a decision comes.</param>
    public PayRunApprovalSubjectHandler(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public string SubjectKind => PayrollConstants.PayRunSubjectKind;

    /// <inheritdoc />
    public Task ClosedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => _services.GetRequiredService<DefaultPayRunService>().ApprovalClosedAsync(request, cancellationToken);
}

/// <summary>
/// Limits who sees and decides the approval requests of pay runs to the employers they may see or approve the runs of.
/// </summary>
public sealed class PayRunSourceAccess : ISourceAccess
{
    private readonly IPayrollScopeService _scope;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunSourceAccess"/> class.
    /// </summary>
    /// <param name="scope">The payroll scope.</param>
    public PayRunSourceAccess(IPayrollScopeService scope)
    {
        _scope = scope;
    }

    /// <inheritdoc />
    public string SourceKind => PayrollConstants.PayRunSubjectKind;

    /// <inheritdoc />
    public async Task<SourceAccessScope> GetScopeAsync(ClaimsPrincipal user, SourceAccessLevel level, CancellationToken cancellationToken = default)
    {
        var scope = await _scope.GetScopeAsync(user, level == SourceAccessLevel.Manage ? PayrollPermissions.ApprovePayRuns : PayrollPermissions.ViewPayRuns, cancellationToken);

        return scope.All ? SourceAccessScope.All : SourceAccessScope.Of(scope.EmployerIds);
    }
}
