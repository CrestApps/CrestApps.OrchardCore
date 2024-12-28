using CrestApps.OrchardCore.OpenAI;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users;

public sealed class OpenAIAdminMenu : AdminNavigationProvider
{
    private readonly IAIChatProfileStore _chatProfileStore;
    internal readonly IStringLocalizer S;

    public OpenAIAdminMenu(
        IAIChatProfileStore chatProfileStore,
        IStringLocalizer<OpenAIAdminMenu> stringLocalizer)
    {
        _chatProfileStore = chatProfileStore;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["OpenAI"], "90", openAI => openAI
                .AddClass("openai")
                .Id("openid")
                .Add(S["Profiles"], "after.5", profiles => profiles
                    .AddClass("openai-profiles")
                    .Id("openAIProfiles")
                    .Action("Index", "Profiles", OpenAIConstants.Feature.Area)
                    .Permission(AIChatPermissions.ManageAIChatProfiles)
                    .LocalNav()
                )
                .Add(S["Deployments"], "after.10", deployments => deployments
                    .AddClass("openai-deployments")
                    .Id("openAIDeployments")
                    .Action("Index", "Deployments", OpenAIConstants.Feature.Area)
                    .Permission(AIChatPermissions.ManageAIChatProfiles)
                    .LocalNav()
                )
            , priority: 1);

        var profiles = await _chatProfileStore.GetAllAsync();

        foreach (var profile in profiles)
        {
            var title = new LocalizedString(profile.Name, profile.Name);

            builder
               .Add(S["OpenAI"], openAI => openAI
                   .Add(title, title.PrefixPosition(), chat => chat
                       .Action("Index", "Admin", new
                       {
                           area = "CrestApps.OrchardCore.OpenAI",
                           profileId = profile.Id,
                       })
                       .Permission(AIChatPermissions.QueryAnyAIChatProfile)
                       .Resource(profile)
                       .LocalNav()
                   )
               );
        }
    }
}
