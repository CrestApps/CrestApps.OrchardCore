using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The persistence contract for <see cref="SmsConversation"/>.
/// </summary>
public interface ISmsConversationStore : ICatalog<SmsConversation>
{
    /// <summary>
    /// Finds the conversation for a given number pair, keyed on the DID we own and the customer's number. This
    /// is the find-or-create key of the inbound pipeline.
    /// </summary>
    /// <param name="serviceAddress">The DID (service address) the thread runs on.</param>
    /// <param name="customerAddress">The customer's number (E.164).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching conversation, or <see langword="null"/> when none exists.</returns>
    Task<SmsConversation> FindByAddressesAsync(string serviceAddress, string customerAddress, CancellationToken cancellationToken = default);

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
}
