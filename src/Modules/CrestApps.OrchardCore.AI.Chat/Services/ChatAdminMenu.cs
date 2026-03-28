using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Mvc.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Services;

public sealed class ChatAdminMenu : AdminNavigationProvider
{
    private readonly IAIProfileStore _profileStore;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    public ChatAdminMenu(
        IAIProfileStore profileStore,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<ChatAdminMenu> stringLocalizer)
    {
        _profileStore = profileStore;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var profiles = await _profileStore.GetByTypeAsync(AIProfileType.Chat);

        builder
           .Add(S["Artificial Intelligence"], artificialIntelligence =>
           {
               var i = 1;
               foreach (var profile in profiles.OrderBy(p => p.DisplayText))
               {
                   var settings = profile.GetSettings<AIChatProfileSettings>();

                   if (!settings.IsOnAdminMenu)
                   {
                       continue;
                   }

                   var name = profile.DisplayText ?? profile.Name;
                   artificialIntelligence
                       .Add(new LocalizedString(name, name), $"chat{i++}", chat => chat
                       .AddClass(profile.Name.HtmlClassify())
                       .Action("Index", "Admin", AIConstants.Feature.Chat, new RouteValueDictionary
                       {
                           { "profileId", profile.ItemId },
                       })
                       .Permission(AIPermissions.QueryAnyAIProfile)
                       .Resource(profile)
                       .LocalNav()
                   );
               }
           });
    }
}

