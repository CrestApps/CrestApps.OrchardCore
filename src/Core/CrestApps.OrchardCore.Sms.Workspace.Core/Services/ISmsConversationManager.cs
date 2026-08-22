using CrestApps.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// The management contract for <see cref="SmsConversation"/>.
/// </summary>
public interface ISmsConversationManager : ICatalogManager<SmsConversation>
{
    /// <summary>
    /// Finds the conversation for a given number pair (DID + customer), loading its handlers.
    /// </summary>
    /// <param name="serviceAddress">The DID (service address) the thread runs on.</param>
    /// <param name="customerAddress">The customer's number (E.164).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching conversation, or <see langword="null"/> when none exists.</returns>
    Task<SmsConversation> FindByAddressesAsync(string serviceAddress, string customerAddress, CancellationToken cancellationToken = default);
}
