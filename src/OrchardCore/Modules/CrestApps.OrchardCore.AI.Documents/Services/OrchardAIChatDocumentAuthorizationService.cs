using System.Security.Claims;
using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Documents.Services;

public sealed class OrchardAIChatDocumentAuthorizationService : IAIChatDocumentAuthorizationService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public OrchardAIChatDocumentAuthorizationService(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> CanManageChatInteractionDocumentsAsync(ClaimsPrincipal user, ChatInteraction interaction)
    {
        if (_httpContextAccessor.HttpContext == null)
        {
            return false;
        }

        if (!await _authorizationService.AuthorizeAsync(user, AIPermissions.EditChatInteractions))
        {
            return false;
        }

        return await _authorizationService.AuthorizeAsync(user, AIPermissions.EditChatInteractions, interaction);
    }

    public async Task<bool> CanManageChatSessionDocumentsAsync(ClaimsPrincipal user, AIProfile profile, AIChatSession session)
    {
        if (_httpContextAccessor.HttpContext == null)
        {
            return false;
        }

        if (!await _authorizationService.AuthorizeAsync(user, AIPermissions.QueryAnyAIProfile))
        {
            return false;
        }

        return await _authorizationService.AuthorizeAsync(user, AIPermissions.QueryAnyAIProfile, profile);
    }
}
