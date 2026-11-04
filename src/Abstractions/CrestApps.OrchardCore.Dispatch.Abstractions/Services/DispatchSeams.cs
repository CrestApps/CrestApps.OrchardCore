using CrestApps.OrchardCore.Dispatch.Models;

namespace CrestApps.OrchardCore.Dispatch.Services;

/// <summary>
/// Solves a day's routing problem: who does which visit, and when, for the least travel. It never writes; its plan
/// becomes a proposal the dispatcher applies.
/// </summary>
public interface IDispatchOptimizer
{
    /// <summary>
    /// Gets the solver's name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Solves a problem.
    /// </summary>
    /// <param name="problem">The problem.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The plan.</returns>
    Task<DispatchPlan> SolveAsync(DispatchProblem problem, CancellationToken cancellationToken = default);
}

/// <summary>
/// Makes proposals for a day from the site's optimizer, and applies them through the dispatch service.
/// </summary>
public interface IDispatchOptimizationService
{
    /// <summary>
    /// Proposes a better arrangement of some technicians' day, leaving pinned and started visits where they are.
    /// </summary>
    /// <param name="day">The day, in the dispatch time zone.</param>
    /// <param name="workerIds">The technicians, or <see langword="null"/> for everyone on shift.</param>
    /// <param name="includeUnscheduled">Whether to fit in visits waiting for a time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The proposal.</returns>
    Task<DispatchProposal> ProposeAsync(DateOnly day, IReadOnlyCollection<string> workerIds, bool includeUnscheduled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies some or all of a proposal's moves, each checked again as it is made.
    /// </summary>
    /// <param name="moves">The moves.</param>
    /// <param name="actor">Who applies them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What was applied.</returns>
    Task<DispatchApplyResult> ApplyAsync(IReadOnlyList<ProposalMove> moves, string actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// A piece of equipment that can be booked for a visit.
/// </summary>
/// <param name="AssetId">The asset.</param>
/// <param name="Name">Its name.</param>
/// <param name="CalendarId">Its calendar.</param>
public sealed record DispatchEquipment(string AssetId, string Name, string CalendarId);

/// <summary>
/// Finds equipment of a kind that is free, such as a lift from Fleet.
/// </summary>
public interface IDispatchEquipmentService
{
    /// <summary>
    /// Lists the bookable equipment of a kind.
    /// </summary>
    /// <param name="assetTypeId">The asset type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The equipment.</returns>
    Task<IReadOnlyList<DispatchEquipment>> ListAsync(string assetTypeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives an asset type's name.
    /// </summary>
    /// <param name="assetTypeId">The asset type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name.</returns>
    Task<string> TypeNameAsync(string assetTypeId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A message to a customer about their visit.
/// </summary>
public sealed class VisitNoticeMessage
{
    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public ServiceVisit Visit { get; set; }

    /// <summary>
    /// Gets or sets the kind of notice, one of <see cref="DispatchConstants.Notices"/>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the phone number to text.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the email address to write to.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the subject, for email.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text { get; set; }
}

/// <summary>
/// Sends a customer notice on one channel, such as SMS or email.
/// </summary>
public interface IVisitNoticeSender
{
    /// <summary>
    /// Gets the channel's name, such as Sms.
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// Tells whether it can reach the customer of a message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>Whether it can.</returns>
    bool CanSend(VisitNoticeMessage message);

    /// <summary>
    /// Sends a message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What went wrong, or <see langword="null"/> when it was sent.</returns>
    Task<string> SendAsync(VisitNoticeMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Makes and reads the customer's tracking links.
/// </summary>
public interface IVisitTrackingLinks
{
    /// <summary>
    /// Makes the token of a visit's tracking link.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <returns>The token.</returns>
    string CreateToken(ServiceVisit visit);

    /// <summary>
    /// Reads a token: the visit it is for, while the link still works.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The visit, or <see langword="null"/> when the link doesn't work or has expired.</returns>
    Task<ServiceVisit> ReadAsync(string token, CancellationToken cancellationToken = default);
}
