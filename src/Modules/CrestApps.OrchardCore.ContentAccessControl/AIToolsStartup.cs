using CrestApps.Core.AI;
using CrestApps.OrchardCore.ContentAccessControl.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContentAccessControl;

/// <summary>
/// Registers the content access AI tools whenever the AI feature is on. Both need <c>EditContent</c> on the item, and
/// setting the roles asks for approval, because it changes who may view the item.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetContentItemAccessRolesTool>(GetContentItemAccessRolesTool.TheName)
            .WithTitle(S["Get Content Item Access Roles"])
            .WithDescription(S["Tells which roles may view a content item and whether its content type restricts access at all (role picker parts with \"Restrict content\" on, including named parts). Use it before changing who can see an item."])
            .WithCategory(S["Content Access Control"])
            .Selectable();

        services.AddCoreAITool<SetContentItemAccessRolesTool>(SetContentItemAccessRolesTool.TheName)
            .WithTitle(S["Set Content Item Access Roles"])
            .WithDescription(S["Sets the roles that may view a content item on a content type that restricts access by role. An empty list makes the item visible to everyone who can view content; it fails when the type has no restricting role picker part."])
            .WithCategory(S["Content Access Control"])
            .AsSecurity()
            .Selectable();
    }
}
