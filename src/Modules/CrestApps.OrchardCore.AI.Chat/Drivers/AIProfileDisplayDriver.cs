using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

internal sealed class AIProfileDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AIProfileDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProfile profile, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIProfile_ChatActionsMenu_SummaryAdmin", profile)
            .Location("ActionsMenu:5")
            .RenderWhen(async () => profile.Type == AIProfileType.Chat && await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        );
    }
}
