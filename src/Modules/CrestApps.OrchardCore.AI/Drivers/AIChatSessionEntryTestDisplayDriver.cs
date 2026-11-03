using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Adds <c>Create test</c> to each conversation in the AI chat sessions list, which makes a simulation test that plays
/// the conversation's customer again.
/// </summary>
public sealed class AIChatSessionEntryTestDisplayDriver : DisplayDriver<AIChatSessionEntry>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionEntryTestDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the current user.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public AIChatSessionEntryTestDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(AIChatSessionEntry session, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid) ||
            _httpContextAccessor.HttpContext?.User is not { } user ||
            !await _authorizationService.AuthorizeAsync(user, AIPermissions.ManageAIQuality))
        {
            return null;
        }

        return View("AIChatSessionEntry_CreateTest_SummaryAdmin", session).Location("ActionsMenu:5");
    }
}
