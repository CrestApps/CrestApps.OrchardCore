using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;
using A2APermissions = CrestApps.OrchardCore.AI.A2A.A2APermissions;

namespace CrestApps.Core.AI.A2A.Services;

internal sealed class A2AAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public A2AAdminMenu(IStringLocalizer<A2AAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Agent to Agent Hosts"], S["Agent to Agent Hosts"].PrefixPosition(), a2a => a2a
                    .AddClass("ai-a2a-connections")
                    .Id("aiA2AConnections")
                    .Action("Index", "Connections", OrchardCore.AI.A2A.A2AConstants.Feature.Area)
                    .Permission(A2APermissions.ManageA2AConnections)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
