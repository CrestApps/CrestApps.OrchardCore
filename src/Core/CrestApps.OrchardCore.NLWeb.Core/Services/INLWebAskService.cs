using System.Security.Claims;
using CrestApps.OrchardCore.NLWeb.Models;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Answers NLWeb questions. Every transport (HTTP, server-sent events, MCP) writes the same stream of events.
/// </summary>
public interface INLWebAskService
{
    /// <summary>
    /// Answers a question.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="user">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The events of the response: a start event, then results and text, then a completion event; or a
    /// failure or a request for more information.</returns>
    IAsyncEnumerable<NLWebEvent> AskAsync(NLWebAskRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
