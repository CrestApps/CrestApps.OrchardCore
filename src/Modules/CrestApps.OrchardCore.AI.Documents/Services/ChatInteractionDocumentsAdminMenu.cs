using CrestApps.OrchardCore.AI.Documents.Drivers;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Documents.Services;

public sealed class ChatInteractionDocumentsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", InteractionDocumentSettingsDisplayDriver.GroupId },
    };


    internal readonly IStringLocalizer S;

    public ChatInteractionDocumentsAdminMenu(IStringLocalizer<ChatInteractionDocumentsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
           .Add(S["Settings"], settings =>
           {
               settings
                   .Add(S["Chat Interactions"], S["Chat Interactions"].PrefixPosition(), chatInteractions => chatInteractions
                       .Action("Index", "Admin", _routeValues)
                       .Permission(AIPermissions.ManageChatInteractionSettings)
                       .LocalNav()
                   );
           });

        return ValueTask.CompletedTask;
    }
}

