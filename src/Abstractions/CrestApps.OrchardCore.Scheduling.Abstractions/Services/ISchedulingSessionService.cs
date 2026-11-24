using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// Keeps dated sessions and their registrations: registering up to the capacity, a waitlist that offers freed places
/// automatically, and attendance by roster.
/// </summary>
public interface ISchedulingSessionService
{
    /// <summary>
    /// Creates or updates a session.
    /// </summary>
    /// <returns>The reasons it was refused; empty when it was saved.</returns>
    Task<IReadOnlyList<string>> SaveAsync(SchedulingSession session, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a session.
    /// </summary>
    Task<SchedulingSession> FindByIdAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the sessions made by a source, such as a course's class.
    /// </summary>
    Task<IReadOnlyList<SchedulingSession>> ListBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the sessions that start in a period.
    /// </summary>
    Task<IReadOnlyList<SchedulingSession>> ListAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a session and every registration and wait of it.
    /// </summary>
    Task<SchedulingSession> CancelAsync(string sessionId, string reason, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a person, or puts them on the waitlist when the session is full and it has one.
    /// </summary>
    Task<SessionRegistrationResult> RegisterAsync(SessionRegistrationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a place that was offered from the waitlist and is still held.
    /// </summary>
    Task<SessionRegistrationResult> AcceptOfferAsync(string appointmentId, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a registration and offers the place to the next person waiting.
    /// </summary>
    Task<SessionRegistrationResult> CancelRegistrationAsync(string appointmentId, string reason, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leaves the waitlist.
    /// </summary>
    Task<bool> LeaveWaitlistAsync(string waitlistEntryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the registrations of a session, cancelled ones included.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListRegistrationsAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a person's registrations in every dated session, cancelled ones included.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListRegistrationsForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the waitlist of a session, first come first.
    /// </summary>
    Task<IReadOnlyList<WaitlistEntry>> ListWaitlistAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the places taken, held offers included.
    /// </summary>
    Task<int> CountTakenAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the attendance of several registrations at once, such as from a roster.
    /// </summary>
    /// <returns>How many registrations changed.</returns>
    Task<int> RecordAttendanceAsync(string sessionId, IEnumerable<SessionAttendanceMark> marks, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Passes the offers that ran out to the next people waiting.
    /// </summary>
    /// <returns>How many offers ran out.</returns>
    Task<int> ExpireOffersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A request to register for a session.
/// </summary>
public sealed class SessionRegistrationRequest
{
    public string SessionId { get; set; }

    public string UserId { get; set; }

    public string CustomerName { get; set; }

    public string CustomerEmail { get; set; }

    public string Source { get; set; }

    public string SourceId { get; set; }

    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets whether to wait for a place when the session is full.
    /// </summary>
    public bool JoinWaitlistWhenFull { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the registration window is ignored, as when staff register someone.
    /// </summary>
    public bool IgnoreWindow { get; set; }
}

/// <summary>
/// What registering did.
/// </summary>
public sealed class SessionRegistrationResult
{
    public Appointment Appointment { get; set; }

    public WaitlistEntry WaitlistEntry { get; set; }

    public string Error { get; set; }

    public bool Succeeded => Error is null && (Appointment is not null || WaitlistEntry is not null);

    public bool Waitlisted => Error is null && Appointment is null && WaitlistEntry is not null;
}

/// <summary>
/// The attendance of one registration.
/// </summary>
/// <param name="AppointmentId">The registration.</param>
/// <param name="Attendance">How the person attended.</param>
/// <param name="MinutesAttended">The minutes attended; empty for all of them when attended, none when absent.</param>
public sealed record SessionAttendanceMark(string AppointmentId, AppointmentAttendance Attendance, int? MinutesAttended = null);
