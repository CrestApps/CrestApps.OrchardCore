using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Manages employers, so their handlers run.
/// </summary>
public interface IEmployerManager : ICatalogManager<Employer>
{
}

/// <summary>
/// Finds the site's own company: the employer Payroll pays the site's staff as, edited under Payroll &gt; Management
/// &gt; Company. A new pay run or pay profile belongs to it unless another employer is chosen with Multiple Employers on.
/// </summary>
public interface IPayrollCompanyService
{
    /// <summary>
    /// Gets the company: the employer the settings name, or the oldest employer.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The company, or <see langword="null"/> before it is set up.</returns>
    Task<Employer> GetCompanyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes an employer the company.
    /// </summary>
    /// <param name="employerId">The employer.</param>
    /// <returns>A task that completes when the settings are saved.</returns>
    Task SetCompanyAsync(string employerId);
}

/// <summary>
/// Tells Payroll that a module uses an employer, so it is not deleted. Register with
/// <c>services.TryAddEnumerable(ServiceDescriptor.Scoped&lt;IEmployerUsage, T&gt;())</c>.
/// </summary>
public interface IEmployerUsage
{
    /// <summary>
    /// Describes what uses the employer, or returns nothing.
    /// </summary>
    /// <param name="employer">The employer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What uses it.</returns>
    Task<IReadOnlyList<string>> GetUsagesAsync(Employer employer, CancellationToken cancellationToken = default);
}

/// <summary>
/// Answers which employers a user may act on with a permission, for pages, data sets, reports and exports alike.
/// </summary>
public interface IPayrollScopeService
{
    /// <summary>
    /// Gets the employers a user may act on with a permission: every one with the global permission, otherwise those
    /// of the per-employer permissions they hold.
    /// </summary>
    /// <param name="user">The user, or <see langword="null"/> for none.</param>
    /// <param name="permission">The global permission, one of <see cref="PayrollPermissions.PerEmployer"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scope.</returns>
    Task<PayrollScope> GetScopeAsync(ClaimsPrincipal user, Permission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a user may act on an employer with a permission.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="permission">The global permission.</param>
    /// <param name="employerId">The employer.</param>
    /// <returns>Whether they may.</returns>
    Task<bool> AuthorizeAsync(ClaimsPrincipal user, Permission permission, string employerId);
}

/// <summary>
/// The employers a user may act on with a permission.
/// </summary>
public sealed class PayrollScope
{
    /// <summary>
    /// Gets a scope with no employer.
    /// </summary>
    public static PayrollScope None { get; } = new(false, []);

    /// <summary>
    /// Gets a scope with every employer.
    /// </summary>
    public static PayrollScope Everyone { get; } = new(true, []);

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollScope"/> class.
    /// </summary>
    /// <param name="all">Whether every employer is in scope.</param>
    /// <param name="employerIds">The employers in scope when not every one is.</param>
    public PayrollScope(bool all, IEnumerable<string> employerIds)
    {
        All = all;
        EmployerIds = new HashSet<string>(employerIds ?? [], StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets whether every employer is in scope.
    /// </summary>
    public bool All { get; }

    /// <summary>
    /// Gets the employers in scope when not every one is.
    /// </summary>
    public IReadOnlySet<string> EmployerIds { get; }

    /// <summary>
    /// Gets whether no employer is in scope.
    /// </summary>
    public bool IsEmpty
        => !All && EmployerIds.Count == 0;

    /// <summary>
    /// Whether an employer is in scope.
    /// </summary>
    /// <param name="employerId">The employer.</param>
    /// <returns>Whether it is.</returns>
    public bool Allows(string employerId)
        => All || (employerId is not null && EmployerIds.Contains(employerId));
}

/// <summary>
/// Finds the staff members an employer pays, so data limited to some employers is limited to their people. Pay profiles
/// answer it.
/// </summary>
public interface IPayrollEmployeeDirectory
{
    /// <summary>
    /// Gets the staff members the employers pay.
    /// </summary>
    /// <param name="employerIds">The employers.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff member ids.</returns>
    Task<IReadOnlySet<string>> GetStaffMemberIdsAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default);
}
