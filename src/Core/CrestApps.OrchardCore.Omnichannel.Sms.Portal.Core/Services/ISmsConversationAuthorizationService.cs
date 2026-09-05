using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Decides whether a caller may perform an operation on one SMS conversation. The SMS portal permission grants
/// access to the workspace; this service decides which threads inside it the caller owns or serves, so an agent
/// cannot read, answer, close, or claim a thread that belongs to another agent or to a queue they do not serve.
/// </summary>
public interface ISmsConversationAuthorizationService
{
    /// <summary>
    /// Determines whether the caller may perform the operation on the conversation.
    /// </summary>
    /// <param name="principal">The calling principal.</param>
    /// <param name="conversation">The conversation being acted on.</param>
    /// <param name="operation">The operation being requested.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the operation is allowed.</returns>
    Task<bool> AuthorizeAsync(
        ClaimsPrincipal principal,
        SmsConversation conversation,
        SmsConversationOperation operation,
        CancellationToken cancellationToken = default);
}
