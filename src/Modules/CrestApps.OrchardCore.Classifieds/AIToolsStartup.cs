using CrestApps.Core.AI;
using CrestApps.OrchardCore.Classifieds.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Classifieds;

/// <summary>
/// Registers the listing moderation AI tools when the AI feature is on, with the permission of Content › Posted
/// listings. Moderating asks for approval because rejecting a listing emails its poster.
/// </summary>
[Feature(ClassifiedsConstants.Feature)]
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

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListClassifiedsTool>(ListClassifiedsTool.TheName)
            .WithTitle(S["List Classifieds"])
            .WithDescription(S["Lists posted listings by status (default: waiting for approval) with search and paging: title, type, poster, place, expiry, featured-until. Use it to see what needs moderation."])
            .WithCategory(S["Classifieds"])
            .Selectable();

        services.AddCoreAITool<ModerateClassifiedTool>(ModerateClassifiedTool.TheName)
            .WithTitle(S["Moderate Classified"])
            .WithDescription(S["Approves a posted listing (publishes it and starts its term from now) or rejects it with an optional reason (takes it down and emails the poster the reason). The poster of a rejected listing can edit it and post it again."])
            .WithCategory(S["Classifieds"])
            .AsExternal()
            .Selectable();
    }
}
