using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Learning.Classes.Services;

/// <summary>
/// Registers enrolled learners in the dated sessions of a class course item. Places, holds, the waitlist and its offers
/// belong to Scheduling; a registration is a Scheduling appointment whose source id is the enrollment and the class
/// item, so Learning keeps no seat counts of its own.
/// </summary>
public interface IClassRegistrationService
{
    /// <summary>
    /// Lists the scheduled sessions of a class item, soonest first.
    /// </summary>
    Task<IReadOnlyList<SchedulingSession>> ListSessionsAsync(string classItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a learner's place for a class item: the registration that is not cancelled, or the waitlist entry that is
    /// still waiting.
    /// </summary>
    Task<ClassRegistration> FindAsync(string enrollmentId, string classItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers an enrolled learner in a session of a class item, or puts them on its waitlist when it is full.
    /// Registering again for the same session returns the place they have.
    /// </summary>
    Task<ClassRegistrationResult> RegisterAsync(string enrollmentId, string classItemId, string sessionId, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts the place a learner was offered from the waitlist.
    /// </summary>
    Task<ClassRegistrationResult> AcceptOfferAsync(string enrollmentId, string classItemId, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives up a learner's place, or takes them off the waitlist. A freed place is offered to the next person waiting.
    /// </summary>
    Task<ClassRegistrationResult> CancelAsync(string enrollmentId, string classItemId, string reason, string actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives up every place of an enrollment, for example when it ends.
    /// </summary>
    /// <returns>How many places were given up.</returns>
    Task<int> CancelAllAsync(string enrollmentId, string reason, string actor = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// A learner's place for a class item.
/// </summary>
/// <param name="Session">The session.</param>
/// <param name="Appointment">The registration, or <see langword="null"/> while waiting.</param>
/// <param name="WaitlistEntry">The waitlist entry, or <see langword="null"/> when registered.</param>
public sealed record ClassRegistration(SchedulingSession Session, Appointment Appointment, WaitlistEntry WaitlistEntry)
{
    /// <summary>
    /// Gets whether the learner was offered a place they have not accepted yet.
    /// </summary>
    public bool IsOffered => Appointment is { Status: AppointmentStatus.Held };

    /// <summary>
    /// Gets whether the learner is waiting for a place.
    /// </summary>
    public bool IsWaiting => Appointment is null && WaitlistEntry is not null;
}

/// <summary>
/// The result of a change to a learner's place.
/// </summary>
public sealed class ClassRegistrationResult
{
    /// <summary>
    /// Gets the learner's place after the change; <see langword="null"/> once it was given up.
    /// </summary>
    public ClassRegistration Registration { get; init; }

    /// <summary>
    /// Gets the reason the change was refused, one of <see cref="ClassRegistrationErrors"/> or a Scheduling message.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Gets whether the change was made.
    /// </summary>
    public bool Succeeded => Error is null;

    /// <summary>
    /// Creates a refused result.
    /// </summary>
    public static ClassRegistrationResult Fail(string error) => new() { Error = error };
}

/// <summary>
/// Why a change to a learner's place was refused.
/// </summary>
public static class ClassRegistrationErrors
{
    public const string EnrollmentNotFound = "EnrollmentNotFound";

    public const string EnrollmentNotActive = "EnrollmentNotActive";

    public const string NotAClassOfTheCourse = "NotAClassOfTheCourse";

    public const string SessionNotFound = "SessionNotFound";

    public const string AlreadyRegistered = "AlreadyRegistered";

    public const string NotRegistered = "NotRegistered";

    public const string NoOffer = "NoOffer";
}
