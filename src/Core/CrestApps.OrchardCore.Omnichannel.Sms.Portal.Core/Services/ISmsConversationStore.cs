using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// The persistence contract for <see cref="SmsConversation"/>.
/// </summary>
public interface ISmsConversationStore : ICatalog<SmsConversation>
{
    /// <summary>
    /// Finds the conversation for a given number pair, keyed on the DID we own and the contact's number. This
    /// is the find-or-create key of the inbound pipeline.
    /// </summary>
    /// <param name="serviceAddress">The DID (service address) the thread runs on.</param>
    /// <param name="contactAddress">The contact's number (E.164).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching conversation, or <see langword="null"/> when none exists.</returns>
    Task<SmsConversation> FindByAddressesAsync(string serviceAddress, string contactAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the most recent conversation with the specified contact, regardless of which of our numbers it
    /// runs on. Used to enforce a single conversation per contact number when an agent starts a conversation.
    /// </summary>
    /// <param name="contactAddress">The contact's number (E.164).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The most recent matching conversation, or <see langword="null"/> when none exists.</returns>
    Task<SmsConversation> FindByContactAsync(string contactAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the conversations assigned to (or owned personally by) the specified agent, most-recent first.
    /// </summary>
    /// <param name="agentId">The agent profile id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent's conversations.</returns>
    Task<IReadOnlyCollection<SmsConversation>> GetForAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the agent's open, assigned conversations.
    /// </summary>
    /// <remarks>
    /// Routing asks this about every candidate agent on the inbound-message path, so it has to stay a count.
    /// Loading the agent's conversations to count them in memory reads years of closed threads to work out a
    /// number the database can produce on its own, and gets slower for every agent as the tenant ages.
    /// </remarks>
    /// <param name="agentId">The agent profile id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<int> CountOpenAssignedAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the open, assigned conversations of several agents at once, keyed by agent.
    /// </summary>
    /// <remarks>
    /// Routing compares every candidate agent in a queue. Asking per agent makes the number of queries follow
    /// the size of the team, so a two-hundred-agent queue costs two hundred round trips to route one message;
    /// this is one, whatever the size of the queue.
    /// </remarks>
    /// <param name="agentIds">The agent profile ids.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyDictionary<string, int>> CountOpenAssignedAsync(
        IReadOnlyCollection<string> agentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the conversations owned by the specified queue, most-recent first.
    /// </summary>
    /// <param name="queueId">The queue id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The queue's conversations.</returns>
    Task<IReadOnlyCollection<SmsConversation>> GetForQueueAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists queue-owned conversations that were routed (push-assigned) to a specific agent and are still
    /// awaiting pickup (their assignment timestamp has not been cleared by the agent engaging). Used by the
    /// reassignment sweep to detect routed conversations the assigned agent has not picked up in time.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The routed, still-unpicked conversations.</returns>
    Task<IReadOnlyCollection<SmsConversation>> GetRoutedAwaitingPickupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists open conversations whose first-response deadline has passed.
    /// </summary>
    /// <param name="nowUtc">The instant to compare deadlines against.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The conversations that are past their first-response deadline.</returns>
    Task<IReadOnlyCollection<SmsConversation>> GetFirstResponseOverdueAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one page of the inbox. Visibility, the active tab, the ordering and the page bound are all applied
    /// by the database, so the inbox costs the same on a tenant with a million threads as on one with a hundred.
    /// </summary>
    /// <param name="query">The page to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The conversations on the page, most recent first.</returns>
    Task<IReadOnlyList<SmsConversation>> QueryAsync(SmsInboxQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the conversations the query matches, for the inbox tab badges and the pager.
    /// </summary>
    /// <param name="query">The query to count.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of matching conversations.</returns>
    Task<int> CountAsync(SmsInboxQuery query, CancellationToken cancellationToken = default);
}
