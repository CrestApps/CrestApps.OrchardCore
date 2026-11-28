using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that finds a page in the admin menu, when the Admin feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Admin)]
public sealed class AdminToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminToolsStartup(IStringLocalizer<AdminToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<FindAdminPageTool>(FindAdminPageTool.TheName)
            .WithTitle(S["Find Admin Page"])
            .WithDescription(S["Searches the admin menu the current user can see and returns matching pages with their menu path and link. Use it to tell the user where something is configured or to give a link to an admin page."])
            .WithCategory(S["Admin"])
            .Selectable();
    }
}
