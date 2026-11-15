using System.Security.Claims;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using OrchardCore.Security.Permissions;
using YesSql.Services;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Reads what Payroll's data sets and reports show: final statements and processed runs of the employers a user may see,
/// by pay date. Drafts are never read: they change until their run is processed.
/// </summary>
public sealed class PayrollReportReader
{
    private readonly IPayrollScopeService _scope;
    private readonly PayStatementStore _statements;
    private readonly PayRunStore _runs;
    private readonly EmployerStore _employers;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollReportReader"/> class.
    /// </summary>
    /// <param name="scope">The payroll scope.</param>
    /// <param name="statements">The statements.</param>
    /// <param name="runs">The runs.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="httpContextAccessor">The request, for the user of a report.</param>
    public PayrollReportReader(IPayrollScopeService scope, PayStatementStore statements, PayRunStore runs, EmployerStore employers, Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _scope = scope;
        _statements = statements;
        _runs = runs;
        _employers = employers;
    }

    /// <summary>
    /// Gets the employers a user may read with a permission.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scope.</returns>
    public Task<PayrollScope> GetScopeAsync(ClaimsPrincipal user, Permission permission, CancellationToken cancellationToken = default)
        => user is null ? Task.FromResult(PayrollScope.None) : _scope.GetScopeAsync(user, permission, cancellationToken);

    /// <summary>
    /// Gets the employers the user of a report may read. A report needs the global View pay runs permission, which the
    /// reports framework checks; a report run in the background (a subscription) has no request and reads every employer.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scope.</returns>
    public async Task<PayrollScope> GetReportScopeAsync(CancellationToken cancellationToken = default)
        => _httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? await _scope.GetScopeAsync(user, PayrollPermissions.ViewPayRuns, cancellationToken)
            : PayrollScope.Everyone;

    /// <summary>
    /// Lists the final statements (reversals included) of the employers in a scope paid in a range of days.
    /// </summary>
    /// <param name="scope">The employers.</param>
    /// <param name="from">The first pay date, or <see langword="null"/>.</param>
    /// <param name="to">The last pay date, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The statements.</returns>
    public async Task<IReadOnlyList<PayStatement>> ListStatementsAsync(PayrollScope scope, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        if (scope.IsEmpty)
        {
            return [];
        }

        var draft = nameof(PayStatementStatus.Draft);
        var fromDay = from is { } start ? PayStatementLineIndex.DayNumber(start) : int.MinValue;
        var toDay = to is { } end ? PayStatementLineIndex.DayNumber(end) : int.MaxValue;
        var employers = scope.EmployerIds.ToArray();

        return scope.All
            ? await _statements.ListAsync(index => index.Status != draft && index.PayDay >= fromDay && index.PayDay <= toDay, cancellationToken)
            : await _statements.ListAsync(index => index.Status != draft && index.PayDay >= fromDay && index.PayDay <= toDay && index.EmployerId.IsIn(employers), cancellationToken);
    }

    /// <summary>
    /// Lists the runs of the employers in a scope with a pay date in a range of days.
    /// </summary>
    /// <param name="scope">The employers.</param>
    /// <param name="from">The first pay date, or <see langword="null"/>.</param>
    /// <param name="to">The last pay date, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The runs.</returns>
    public async Task<IReadOnlyList<PayRun>> ListRunsAsync(PayrollScope scope, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        if (scope.IsEmpty)
        {
            return [];
        }

        var employers = scope.EmployerIds.ToArray();
        var runs = scope.All
            ? await _runs.ListAsync(index => index.EmployerId != null, cancellationToken)
            : await _runs.ListAsync(index => index.EmployerId.IsIn(employers), cancellationToken);

        return [.. runs.Where(run => (from is null || run.PayDate >= from) && (to is null || run.PayDate <= to))];
    }

    /// <summary>
    /// Gets the employers' names by id.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The names.</returns>
    public async Task<IReadOnlyDictionary<string, string>> EmployerNamesAsync(CancellationToken cancellationToken = default)
        => (await _employers.GetAllAsync(cancellationToken)).ToDictionary(employer => employer.ItemId, employer => employer.Name, StringComparer.Ordinal);
}
