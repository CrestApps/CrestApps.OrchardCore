using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// A supervisor on an agent's own phone call: a number the agent dialed from the soft phone's keypad, or an extension
/// call. The live dashboard names such a call with a <see cref="PhoneCallKey"/> where it names an interaction for a
/// Contact Center call, and every engagement action on it comes here.
/// </summary>
public interface IContactCenterPhoneCallSupervisionService
{
    /// <summary>
    /// Finds the phone calls the given users are on right now, as either party.
    /// </summary>
    /// <param name="userIds">The users.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Each user's call, by user.</returns>
    Task<IReadOnlyDictionary<string, AgentPhoneCall>> FindCallsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a phone call can be supervised by this provider at all, from what is known of it without asking the provider.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <returns>The modes the provider offers on it; empty when it cannot be monitored.</returns>
    IReadOnlyCollection<MonitorMode> GetAvailableModes(AgentPhoneCall call);

    /// <summary>
    /// Whether the supervisor may act on the agent the key names: the agent works one of the queues the supervisor oversees.
    /// </summary>
    /// <param name="principal">The supervisor.</param>
    /// <param name="supervisorUserId">The supervisor's user.</param>
    /// <param name="key">The phone call key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the supervisor may.</returns>
    Task<bool> IsAuthorizedAsync(ClaimsPrincipal principal, string supervisorUserId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the supervisor's engagement on a phone call.
    /// </summary>
    /// <param name="key">The phone call key.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The engagement, or <see langword="null"/>.</returns>
    Task<PhoneCallEngagement> FindEngagementAsync(string key, string supervisorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts listening to, coaching or joining an agent's phone call on the supervisor's own soft phone.
    /// </summary>
    Task<SupervisorEngagementResult> EngageAsync(string key, string supervisorUserId, ClaimsPrincipal principal, MonitorMode mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the supervisor's engagement on a phone call to another mode, on the same leg.
    /// </summary>
    Task<SupervisorEngagementResult> SwitchModeAsync(string key, string supervisorUserId, ClaimsPrincipal principal, MonitorMode mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the supervisor's engagement on a phone call; the call goes on as it was.
    /// </summary>
    Task<SupervisorEngagementResult> StopAsync(string key, string supervisorUserId, ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a phone call over from the agent: the other party is left talking to the supervisor.
    /// </summary>
    Task<SupervisorEngagementResult> TakeOverAsync(string key, string supervisorUserId, ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends a phone call for everyone on it.
    /// </summary>
    Task<SupervisorEngagementResult> EndCallAsync(string key, string supervisorUserId, ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets every supervisor still listening to a phone call go, once the call has ended.
    /// </summary>
    /// <param name="callId">The call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReleaseCallAsync(string callId, CancellationToken cancellationToken = default);
}
