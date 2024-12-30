using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.OpenAI.Services;

public sealed class OpenAIAdminChatMenu : AdminNavigationProvider
{
    private readonly IAIChatProfileStore _chatProfileStore;

    internal readonly IStringLocalizer S;

    public OpenAIAdminChatMenu(
        IAIChatProfileStore chatProfileStore,
        IStringLocalizer<OpenAIAdminChatMenu> stringLocalizer)
    {
        _chatProfileStore = chatProfileStore;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var profiles = await _chatProfileStore.GetAllAsync();

        builder
           .Add(S["OpenAI"], openAI =>
           {
               var i = 1;
               foreach (var profile in profiles)
               {
                   openAI
                   .Add(new LocalizedString(profile.Name, profile.Name), $"chat{i++}", chat => chat
                       .AddClass(profile.Name.HtmlClassify())
                       .Action("Index", "AdminChat", OpenAIConstants.Feature.Area, new RouteValueDictionary
                       {
                           { "profileId", profile.Id},
                       })
                       .Permission(AIChatPermissions.QueryAnyAIChatProfile)
                       .Resource(profile)
                       .LocalNav()
                   );
               }
           });


        builder
            .Add(S["OpenAI"], openAI => openAI
                .Add(S["Profiles"], "after.5", profiles => profiles
                    .AddClass("openai-profiles")
                    .Id("openAIProfiles")
                    .Action("Index", "ChatProfiles", OpenAIConstants.Feature.Area)
                    .Permission(AIChatPermissions.ManageAIChatProfiles)
                    .LocalNav()
                )
            );
    }
}
