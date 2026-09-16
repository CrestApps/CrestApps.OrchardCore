using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// The management contract for <see cref="SmsConversation"/>.
/// </summary>
public interface ISmsConversationManager : ICatalogManager<SmsConversation>
{
    /// <summary>
    /// Finds the conversation for a given number pair (DID + contact), loading its handlers.
    /// </summary>
    /// <param name="serviceAddress">The DID (service address) the thread runs on.</param>
    /// <param name="contactAddress">The contact's number (E.164).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching conversation, or <see langword="null"/> when none exists.</returns>
    Task<SmsConversation> FindByAddressesAsync(string serviceAddress, string contactAddress, CancellationToken cancellationToken = default);
}
