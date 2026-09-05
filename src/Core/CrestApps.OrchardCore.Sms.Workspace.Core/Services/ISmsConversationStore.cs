using CrestApps.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

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
}
