using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for a row of the chat history list of an AI profile.
/// </summary>
internal sealed class AIChatSessionEntryDisplayDriver : DisplayDriver<AIChatSessionEntry>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionEntryDisplayDriver"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public AIChatSessionEntryDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(AIChatSessionEntry session, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = session.Title?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("AIChatSessionEntry_Fields_SummaryAdmin", session)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AIChatSessionEntry_Buttons_SummaryAdmin", session).Location("Actions:5"),
            View("AIChatSessionEntry_DefaultTags_SummaryAdmin", session)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("AIChatSessionEntry_DefaultMeta_SummaryAdmin", session)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "LastActivity:5"),

            // A session without a user is system-owned (an automated SMS or voice conversation) and is not deleted from here.
            View("AIChatSessionEntry_ActionsMenu_SummaryAdmin", session)
                .Location("ActionsMenu:10")
                .RenderWhen(async () => !string.IsNullOrEmpty(session.UserId) && await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.DeleteChatSession))
        );
    }
}
