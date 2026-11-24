using CrestApps.Core.AI;
using CrestApps.OrchardCore.Fundraising.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Fundraising;

/// <summary>
/// Registers the fundraising AI tool when the AI feature is on: a campaign's progress and donations, with the
/// permission of the fundraising settings.
/// </summary>
[Feature(FundraisingConstants.Feature)]
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
        services.AddCoreAITool<GetFundraisingCampaignProgressTool>(GetFundraisingCampaignProgressTool.TheName)
            .WithTitle(S["Get Fundraising Campaign Progress"])
            .WithDescription(S["Returns a campaign's amount raised, goal, percent, number of donors and days left, and optionally its donations (donor, amount, receipt number, tribute, refunded). Use it to report on a campaign; the content tools return only the campaign's settings."])
            .WithCategory(S["Fundraising"])
            .Selectable();
    }
}
