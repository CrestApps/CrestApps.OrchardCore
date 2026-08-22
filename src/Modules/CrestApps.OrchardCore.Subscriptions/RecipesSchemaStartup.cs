using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Subscriptions.Schemas;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Registers recipe schema contributors for the subscriptions feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, SubscriptionPartSchemaDefinition>();
        services.AddScoped<IContentSchemaDefinition, SubscriptionSummaryPartSchemaDefinition>();
    }
}
