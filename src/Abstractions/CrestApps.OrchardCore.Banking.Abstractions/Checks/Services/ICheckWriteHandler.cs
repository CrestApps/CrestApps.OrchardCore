using CrestApps.OrchardCore.Banking.Checks.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Services;

/// <summary>
/// Takes part in writing a check: every <see cref="ICheckService.IssueAsync"/>, from <b>Write a check</b>, from a module
/// that pays its own records by check (such as Payroll), or from a reissue. Before the check is saved, a handler may
/// complete or change the request (give it a <see cref="CheckIssueRequest.SourceKind"/> and
/// <see cref="CheckIssueRequest.SourceId"/>, an amount from its own lines) or refuse it; after it is saved, in the same
/// unit of work, it does its own work, such as posting the payment in the books. A handler looks at the request's source
/// kind and leaves alone the checks of other modules. Register one with
/// <see cref="ChecksServiceCollectionExtensions.AddCheckWriteHandler{THandler}"/>; derive from
/// <see cref="CheckWriteHandlerBase"/> to implement only one step.
/// </summary>
public interface ICheckWriteHandler
{
    /// <summary>
    /// Called before a check is written, before Banking checks the request. Change
    /// <see cref="CheckWritingContext.Request"/> to complete it, or call <see cref="CheckWritingContext.Refuse"/> to stop
    /// it: nothing is saved, and the reasons are shown with Banking's own. Do no work here that can't be undone; do it in
    /// <see cref="WrittenAsync"/>.
    /// </summary>
    /// <param name="context">The request and, for a reissue, the check it replaces.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task WritingAsync(CheckWritingContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after the check is saved, in the same unit of work and before the <see cref="ICheckObserver"/>s are told.
    /// The check has its id (and its number, unless it waits for a print run). An exception here fails the request, and
    /// the host's unit of work is not committed.
    /// </summary>
    /// <param name="context">The check, the request it was written from and, for a reissue, the check it replaced.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task WrittenAsync(CheckWrittenContext context, CancellationToken cancellationToken = default);
}
