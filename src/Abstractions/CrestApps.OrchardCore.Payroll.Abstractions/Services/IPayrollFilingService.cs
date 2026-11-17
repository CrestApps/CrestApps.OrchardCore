using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Manages tax deposits.
/// </summary>
public interface ITaxDepositManager : ICatalogManager<TaxDeposit>
{
    /// <summary>
    /// Lists an employer's deposits of a span of periods.
    /// </summary>
    /// <param name="employerId">The employer.</param>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The deposits whose period ends in the span.</returns>
    Task<IReadOnlyList<TaxDeposit>> ListAsync(string employerId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages filings.
/// </summary>
public interface IPayrollFilingManager : ICatalogManager<PayrollFiling>
{
    /// <summary>
    /// Lists the filings of some employers, or of all when <paramref name="employerIds"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="employerIds">The employers.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The filings.</returns>
    Task<IReadOnlyList<PayrollFiling>> ListByEmployersAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the filing of a form for a period that is not amended (the draft or the filed one), or <see langword="null"/>.
    /// </summary>
    /// <param name="employerId">The employer.</param>
    /// <param name="form">The form.</param>
    /// <param name="year">The year.</param>
    /// <param name="quarter">The quarter, or 0.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The filing.</returns>
    Task<PayrollFiling> FindCurrentAsync(string employerId, string form, int year, int quarter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Works out the tax liabilities of an employer from its final statements and deposits.
/// </summary>
public interface ITaxLiabilityService
{
    /// <summary>
    /// Lists the liability periods whose pay dates are in a span, with what was deposited and when each is due.
    /// </summary>
    /// <param name="employerId">The employer.</param>
    /// <param name="from">The first pay date.</param>
    /// <param name="to">The last pay date.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The periods, by due date.</returns>
    Task<IReadOnlyList<TaxLiabilityPeriod>> ListAsync(string employerId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <summary>
/// A tax form a country pack prepares from the final statements, such as a quarterly return or the year's wage
/// statements. Register one with <c>services.AddPayrollFilingForm&lt;T&gt;()</c>.
/// </summary>
public interface IPayrollFilingForm
{
    /// <summary>Gets the stored name, such as US-941.</summary>
    string Name { get; }

    /// <summary>Gets what people see.</summary>
    LocalizationSource DisplayName { get; }

    /// <summary>Gets the jurisdiction an employer must be registered in for the form.</summary>
    string Jurisdiction { get; }

    /// <summary>Gets whether the form is for a quarter (otherwise a year).</summary>
    bool IsQuarterly { get; }

    /// <summary>Gets whether the form has a statement per person, sent to each person.</summary>
    bool HasPersonStatements { get; }

    /// <summary>
    /// Prepares the form's boxes (and each person's) from the period's statements.
    /// </summary>
    /// <param name="context">The period, its statements and deposits.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task PrepareAsync(PayrollFilingContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a form is prepared from.
/// </summary>
public sealed class PayrollFilingContext
{
    /// <summary>Gets the employer.</summary>
    public required Employer Employer { get; init; }

    /// <summary>Gets the filing to fill: its boxes, people and notes are cleared first.</summary>
    public required PayrollFiling Filing { get; init; }

    /// <summary>Gets the first pay date of the period.</summary>
    public required DateOnly From { get; init; }

    /// <summary>Gets the last pay date of the period.</summary>
    public required DateOnly To { get; init; }

    /// <summary>Gets the final statements with pay dates in the period, reversals included.</summary>
    public required IReadOnlyList<PayStatement> Statements { get; init; }

    /// <summary>Gets the pay codes, by id.</summary>
    public required IReadOnlyDictionary<string, PayCode> Codes { get; init; }

    /// <summary>Gets the pay profiles of the people in the statements, by id.</summary>
    public required IReadOnlyDictionary<string, EmployeePayProfile> Profiles { get; init; }

    /// <summary>Gets the deposits of the period.</summary>
    public required IReadOnlyList<TaxDeposit> Deposits { get; init; }

    /// <summary>Gets the services, for what a form needs more.</summary>
    public required IServiceProvider Services { get; init; }
}

/// <summary>
/// Prepares, files and amends filings, and closes a year.
/// </summary>
public interface IPayrollFilingService
{
    /// <summary>
    /// Gets the forms an employer can file: those of the jurisdictions it is registered in.
    /// </summary>
    /// <param name="employer">The employer.</param>
    /// <returns>The forms.</returns>
    IReadOnlyList<IPayrollFilingForm> FormsFor(Employer employer);

    /// <summary>
    /// Prepares a form for a period: a new draft, or the draft prepared again. A filed form is amended instead.
    /// </summary>
    /// <param name="employer">The employer.</param>
    /// <param name="form">The form's name.</param>
    /// <param name="year">The year.</param>
    /// <param name="quarter">The quarter of a quarterly form, or 0.</param>
    /// <param name="user">Who prepares it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The filing, or the errors.</returns>
    Task<PayrollFilingResult> PrepareAsync(Employer employer, string form, int year, int quarter, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a draft as filed: it never changes again, and people with a statement in it are told it is ready.
    /// </summary>
    /// <param name="filing">The filing.</param>
    /// <param name="filedOn">The day it was filed.</param>
    /// <param name="reference">The agency's confirmation number.</param>
    /// <param name="user">Who filed it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<PayrollFilingResult> MarkFiledAsync(PayrollFiling filing, DateOnly filedOn, string reference, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the correction of a filed form: a new draft, prepared again from the statements, that names it.
    /// </summary>
    /// <param name="filing">The filed filing.</param>
    /// <param name="user">Who amends it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The new draft, or the errors.</returns>
    Task<PayrollFilingResult> AmendAsync(PayrollFiling filing, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets what keeps an employer's year from being closed, such as a pay run not paid yet.
    /// </summary>
    /// <param name="employer">The employer.</param>
    /// <param name="year">The year.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The blockers.</returns>
    Task<IReadOnlyList<string>> YearEndBlockersAsync(Employer employer, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes an employer's year: no pay run with a pay date in it is opened, calculated or processed, and no statement
    /// of it is voided. The year's wage statements are prepared when the employer files any.
    /// </summary>
    /// <param name="employer">The employer.</param>
    /// <param name="year">The year.</param>
    /// <param name="user">Who closes it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<PayrollFilingResult> CloseYearAsync(Employer employer, int year, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens a closed year.
    /// </summary>
    /// <param name="employer">The employer.</param>
    /// <param name="year">The year.</param>
    /// <param name="user">Who reopens it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<PayrollFilingResult> ReopenYearAsync(Employer employer, int year, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of a filing action.
/// </summary>
public sealed class PayrollFilingResult
{
    /// <summary>Gets or sets the filing.</summary>
    public PayrollFiling Filing { get; set; }

    /// <summary>Gets the errors.</summary>
    public IList<string> Errors { get; } = [];

    /// <summary>Gets whether it succeeded.</summary>
    public bool Succeeded
        => Errors.Count == 0;
}
