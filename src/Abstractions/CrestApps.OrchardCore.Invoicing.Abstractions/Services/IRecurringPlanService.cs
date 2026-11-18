using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Stores and finds recurring plans. A plan changes through the catalog while it is not completed or canceled; a change
/// applies to the runs after it.
/// </summary>
public interface IRecurringPlanManager : ICatalogManager<RecurringPlan>
{
    /// <summary>
    /// Returns a page of plans, newest first.
    /// </summary>
    Task<PageResult<RecurringPlan>> PageAsync(int page, int pageSize, RecurringPlanQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// The filters of a list of recurring plans.
/// </summary>
public sealed class RecurringPlanQuery
{
    /// <summary>
    /// Gets or sets text matched against the name, the customer's name and the reference.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status to show.
    /// </summary>
    public RecurringPlanStatus? Status { get; set; }
}

/// <summary>
/// Starts, pauses, resumes and cancels recurring plans, and makes their invoices when they are due. Each command takes
/// the plan's lock; a run is idempotent per plan and occurrence. The service checks no permission.
/// </summary>
public interface IRecurringPlanService
{
    /// <summary>
    /// Starts a draft plan: works out its first run from its start date and rule.
    /// </summary>
    Task<RecurringPlanResult> ActivateAsync(string planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an active plan. The runs due while it is paused are skipped.
    /// </summary>
    Task<RecurringPlanResult> PauseAsync(string planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused plan from its next occurrence on or after <paramref name="today"/>.
    /// </summary>
    Task<RecurringPlanResult> ResumeAsync(string planId, DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a plan for good.
    /// </summary>
    Task<RecurringPlanResult> CancelAsync(string planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the invoices of a plan's runs due on or before <paramref name="today"/>.
    /// </summary>
    Task<RecurringPlanResult> RunAsync(string planId, DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the invoices of every active plan's runs due on or before <paramref name="today"/>, and returns how many
    /// invoices were made.
    /// </summary>
    Task<int> RunDueAsync(DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the next occurrences of a plan from its next run (or its start, for a draft), at most
    /// <paramref name="count"/>, within its end.
    /// </summary>
    IReadOnlyList<DateOnly> GetUpcoming(RecurringPlan plan, int count);
}

/// <summary>
/// What a recurring plan command did.
/// </summary>
public sealed class RecurringPlanResult
{
    /// <summary>
    /// Gets whether the command did what it was asked.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the plan as it is after the command.
    /// </summary>
    public RecurringPlan Plan { get; set; }

    /// <summary>
    /// Gets the invoices the command made.
    /// </summary>
    public IList<Invoice> Invoices { get; } = [];

    /// <summary>
    /// Gets why the command was refused.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Adds a reason the command was refused.
    /// </summary>
    public RecurringPlanResult Fail(string message)
    {
        Errors.Add(message);

        return this;
    }
}

/// <summary>
/// Hears the runs of recurring plans, after they are saved. Every member has a default that does nothing.
/// </summary>
public interface IRecurringPlanObserver
{
    /// <summary>
    /// Called after a run made an invoice.
    /// </summary>
    Task GeneratedAsync(RecurringPlan plan, Invoice invoice, RecurringPlanRun run, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after a run failed.
    /// </summary>
    Task RunFailedAsync(RecurringPlan plan, RecurringPlanRun run, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
