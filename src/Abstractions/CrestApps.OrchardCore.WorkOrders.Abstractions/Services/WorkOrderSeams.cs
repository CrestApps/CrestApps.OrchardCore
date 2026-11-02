using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// Gives the hours a service level clock runs in.
/// </summary>
public interface IServiceHoursProvider
{
    /// <summary>
    /// Gives the open stretches of a business hours calendar between two times.
    /// </summary>
    /// <param name="calendarId">The calendar.</param>
    /// <param name="timeZoneId">Its time zone.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The open stretches, in order, or <see langword="null"/> when the provider doesn't know the calendar.</returns>
    Task<IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)>> GetOpenAsync(string calendarId, string timeZoneId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}

/// <summary>
/// Takes the parts used on a work order off a stock location.
/// </summary>
public interface IWorkOrderPartsService
{
    /// <summary>
    /// Records parts used, taking them off the location.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="line">The line, with its stock item and location.</param>
    /// <param name="quantity">How many.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Why it was refused, or <see langword="null"/>.</returns>
    Task<string> UseAsync(WorkOrder workOrder, WorkOrderLine line, decimal quantity, string actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Invoices a completed work order.
/// </summary>
public interface IWorkOrderBilling
{
    /// <summary>
    /// Creates the invoice.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The invoice's transaction id.</returns>
    Task<string> InvoiceAsync(WorkOrder workOrder, decimal amount, string currency, CancellationToken cancellationToken = default);
}

/// <summary>
/// Hears what happens to service agreements.
/// </summary>
public interface IServiceAgreementObserver
{
    /// <summary>
    /// Runs once when an active agreement comes within its notice of its end date.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task.</returns>
    Task OnExpiringAsync(ServiceAgreement agreement, CancellationToken cancellationToken = default);
}

/// <summary>
/// Hears what happens to work orders, after the work is saved.
/// </summary>
public interface IWorkOrderObserver
{
    /// <summary>
    /// Called for each change.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="eventName">The event, one of <see cref="WorkOrdersConstants.Events"/>.</param>
    /// <param name="detail">The detail, such as the old status.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task.</returns>
    Task OnEventAsync(WorkOrder workOrder, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default);
}
