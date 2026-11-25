using CrestApps.Core.AI;
using CrestApps.OrchardCore.Stripe.AI;
using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Stripe;

/// <summary>
/// Registers the Stripe AI tools when the Stripe and AI features are on. Both need <c>ManageStripeSettings</c>, as
/// Settings › Payments › Stripe. Disconnecting asks for approval. There is no connect tool: connecting needs the
/// secret key, which never passes through a chat.
/// </summary>
[Feature(StripeConstants.Feature.ModuleId)]
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
        services.AddCoreAITool<GetStripeConnectionStatusTool>(GetStripeConnectionStatusTool.TheName)
            .WithTitle(S["Get Stripe Connection Status"])
            .WithDescription(S["Reports whether Stripe is in live or test mode, whether an account is connected for each, the connected account id, and which keys are set and whether they come from the site or from application configuration. No key or secret is ever returned."])
            .WithCategory(S["Payments"])
            .Selectable();

        services.AddCoreAITool<DisconnectStripeTool>(DisconnectStripeTool.TheName)
            .WithTitle(S["Disconnect Stripe"])
            .WithDescription(S["Disconnects the Stripe account of the live or the test environment: removes the webhook at Stripe and clears the stored keys for it. Checkout can no longer take card payments in that environment until an administrator connects again on the Stripe settings page."])
            .WithCategory(S["Payments"])
            .AsSecurity()
            .Selectable();
    }
}
