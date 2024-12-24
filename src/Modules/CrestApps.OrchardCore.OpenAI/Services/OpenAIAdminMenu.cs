using CrestApps.OrchardCore.OpenAI.Azure.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users;

public sealed class OpenAIAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public OpenAIAdminMenu(IStringLocalizer<OpenAIAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["OpenAI"], "90", openAI => openAI
                .AddClass("openai")
                .Id("openid")
                .Add(S["Profiles"], S["Profiles"].PrefixPosition("1"), profiles => profiles
                    .AddClass("openai-profiles")
                    .Id("openAIProfiles")
                    .Action("Index", "Admin", "CrestApps.OrchardCore.OpenAI")
                    .Permission(AIChatProfilePermissions.ManageAIChatProfiles)
                    .LocalNav()
                )
            , priority: 1);

        return ValueTask.CompletedTask;
    }
}
