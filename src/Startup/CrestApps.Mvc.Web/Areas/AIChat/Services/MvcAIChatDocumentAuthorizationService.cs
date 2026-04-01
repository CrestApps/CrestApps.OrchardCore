using System.Security.Claims;
using CrestApps.AI;
using CrestApps.AI.Models;

namespace CrestApps.Mvc.Web.Services;

public sealed class MvcAIChatDocumentAuthorizationService : IAIChatDocumentAuthorizationService
{
    public Task<bool> CanManageChatInteractionDocumentsAsync(ClaimsPrincipal user, ChatInteraction interaction)
        => Task.FromResult(user?.IsInRole("Administrator") == true);
    public Task<bool> CanManageChatSessionDocumentsAsync(ClaimsPrincipal user, AIProfile profile, AIChatSession session)
        => Task.FromResult(user?.Identity?.IsAuthenticated == true);
}
