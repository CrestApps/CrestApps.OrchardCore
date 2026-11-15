using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Stores and finds estimates. Drafts change through the catalog; a sent estimate changes only through
/// <see cref="IEstimateCommandService"/>, and a change to its offer makes a revision.
/// </summary>
public interface IEstimateManager : ICatalogManager<Estimate>
{
    /// <summary>
    /// Returns a page of estimates, newest first.
    /// </summary>
    Task<PageResult<Estimate>> PageAsync(int page, int pageSize, EstimateQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// The filters of a list of estimates.
/// </summary>
public sealed class EstimateQuery
{
    /// <summary>
    /// Gets or sets text matched against the number, the customer's name and the reference.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status to show.
    /// </summary>
    public EstimateStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the parties billed, when only theirs are shown.
    /// </summary>
    public IList<PartyRef> BillTo { get; set; } = [];
}

/// <summary>
/// The commands that move an estimate on: send it, record the customer's answer, revise, cancel or expire it, and turn
/// it into an invoice. Each takes the estimate's lock, is idempotent and returns its reasons rather than throwing. The
/// service checks no permission.
/// </summary>
public interface IEstimateCommandService
{
    /// <summary>
    /// Sends a draft: numbers it (a revision after the estimate it revises) and locks its offer.
    /// </summary>
    Task<EstimateCommandResult> SendAsync(string estimateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the customer's acceptance of a sent estimate that has not expired, and invoices what
    /// <see cref="Estimate.OnAcceptance"/> says.
    /// </summary>
    Task<EstimateCommandResult> AcceptAsync(string estimateId, EstimateAcceptanceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the customer declined a sent estimate.
    /// </summary>
    Task<EstimateCommandResult> DeclineAsync(string estimateId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws an estimate that was not invoiced.
    /// </summary>
    Task<EstimateCommandResult> CancelAsync(string estimateId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a revision of a sent, declined or expired estimate: a draft with its offer, numbered after it when sent.
    /// The estimate it revises is canceled and points at the revision.
    /// </summary>
    Task<EstimateCommandResult> ReviseAsync(string estimateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns a sent or accepted estimate into a draft invoice: the whole of it, or its deposit as a prepayment invoice.
    /// Converting again returns the invoice made the first time.
    /// </summary>
    Task<EstimateCommandResult> ConvertAsync(string estimateId, EstimateConversion conversion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoices part of a sent or accepted estimate (progress invoicing): a percent of each line chosen, on a draft
    /// invoice whose lines bill that share of the line's quantity. A line is never invoiced past 100% across the
    /// estimate's invoices that are not void; the estimate is <see cref="EstimateStatus.Invoiced"/> once every line is
    /// fully invoiced.
    /// </summary>
    /// <param name="estimateId">The estimate.</param>
    /// <param name="percentByLine">The percent of each line to invoice now, by the line's id; lines left out are not invoiced.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<EstimateCommandResult> InvoiceProgressAsync(string estimateId, IReadOnlyDictionary<string, decimal> percentByLine, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets how much of each line of an estimate is invoiced, in percent, by the line's id, across its invoices that are
    /// not void.
    /// </summary>
    /// <param name="estimateId">The estimate.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyDictionary<string, decimal>> GetInvoicedPercentsAsync(string estimateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires the sent estimates whose expiry date is before <paramref name="today"/>, and returns how many.
    /// </summary>
    Task<int> ExpireDueAsync(DateOnly today, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the customer gives when accepting an estimate.
/// </summary>
public sealed class EstimateAcceptanceRequest
{
    /// <summary>
    /// Gets or sets the name the customer typed; required.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the address they accepted from.
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the signed-in user, if any.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the day it is in the site's time zone, which decides whether the estimate has expired.
    /// </summary>
    public DateOnly Today { get; set; }
}

/// <summary>
/// What an estimate command did.
/// </summary>
public sealed class EstimateCommandResult
{
    /// <summary>
    /// Gets whether the command did what it was asked.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the estimate as it is after the command: the new revision for a revise.
    /// </summary>
    public Estimate Estimate { get; set; }

    /// <summary>
    /// Gets or sets the revision a revise made.
    /// </summary>
    public Estimate Revision { get; set; }

    /// <summary>
    /// Gets or sets the invoice the command made, if it made one.
    /// </summary>
    public Invoice Invoice { get; set; }

    /// <summary>
    /// Gets or sets whether the command had been done already.
    /// </summary>
    public bool AlreadyDone { get; set; }

    /// <summary>
    /// Gets why the command was refused.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Adds a reason the command was refused.
    /// </summary>
    public EstimateCommandResult Fail(string message)
    {
        Errors.Add(message);

        return this;
    }
}

/// <summary>
/// Hears what happens to estimates, after it is saved. Every member has a default that does nothing.
/// </summary>
public interface IEstimateObserver
{
    /// <summary>
    /// Called after an estimate is sent.
    /// </summary>
    Task SentAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after the customer accepts an estimate.
    /// </summary>
    Task AcceptedAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after the customer declines an estimate.
    /// </summary>
    Task DeclinedAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after an estimate expires.
    /// </summary>
    Task ExpiredAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after an estimate is turned into an invoice.
    /// </summary>
    Task ConvertedAsync(Estimate estimate, Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
