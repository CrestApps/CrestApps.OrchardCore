using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.OpenAI.Services;

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
                .Add(S["Deployments"], "after.10", deployments => deployments
                    .AddClass("openai-deployments")
                    .Id("openAIDeployments")
                    .Action("Index", "Deployments", OpenAIConstants.Feature.Area)
                    .Permission(OpenAIChatPermissions.ManageModelDeployments)
                    .LocalNav()
                )
            , priority: 1);

        return ValueTask.CompletedTask;
    }
}
