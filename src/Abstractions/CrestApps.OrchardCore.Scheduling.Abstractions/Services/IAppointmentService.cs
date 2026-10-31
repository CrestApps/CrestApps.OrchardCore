using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// Books, changes and finds appointments. Every booking and reschedule checks the time again under a lock on each resource,
/// and saves the resources with a concurrency check, so two requests can never book the same resource at the same time.
/// </summary>
public interface IAppointmentService
{
    /// <summary>
    /// Books an appointment, or holds its time.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<AppointmentResult> BookAsync(AppointmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a held or requested appointment.
    /// </summary>
    /// <param name="appointment">The appointment.</param>
    /// <param name="actor">Who confirms it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<AppointmentResult> ConfirmAsync(Appointment appointment, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an appointment to another time.
    /// </summary>
    /// <param name="appointment">The appointment.</param>
    /// <param name="startUtc">The new start, in UTC.</param>
    /// <param name="resourceId">A resource to move it to, or <see langword="null"/> to keep or let the type pick.</param>
    /// <param name="actor">Who moves it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<AppointmentResult> RescheduleAsync(Appointment appointment, DateTime startUtc, string resourceId = null, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an appointment.
    /// </summary>
    /// <param name="appointment">The appointment.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who cancels it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<AppointmentResult> CancelAsync(Appointment appointment, string reason = null, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that an appointment took place, or that the customer didn't come.
    /// </summary>
    /// <param name="appointment">The appointment.</param>
    /// <param name="noShow">Whether the customer didn't come.</param>
    /// <param name="actor">Who records it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<AppointmentResult> CompleteAsync(Appointment appointment, bool noShow, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends holds that ran out.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many expired.</returns>
    Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an appointment by id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The appointment, or <see langword="null"/>.</returns>
    Task<Appointment> FindByIdAsync(string id, CancellationToken cancellationToken = default);
}
