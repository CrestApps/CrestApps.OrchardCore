using CrestApps.OrchardCore.OpenAI;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
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
                .Add(S["Profiles"], "after", profiles => profiles
                    .AddClass("openai-profiles")
                    .Id("openAIProfiles")
                    .Action("Index", "Profiles", "CrestApps.OrchardCore.OpenAI")
                    .Permission(AIChatProfilePermissions.ManageAIChatProfiles)
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
                       .Permission(AIChatProfilePermissions.QueryAnyAIChatProfiles)
                       .Resource(profile)
                       .LocalNav()
                   )
               );
        }
    }
}
