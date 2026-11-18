using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Transactions.Deployments;
using CrestApps.OrchardCore.Transactions.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Describes the payment terms recipe and deployment steps to CrestApps Recipes.
/// </summary>
[Feature(TransactionsConstants.Features.PaymentTerms)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class PaymentTermsRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, PaymentTermsRecipeStepSchema>();
        services.AddDeploymentStepSchema<PaymentTermsDeploymentStepSchema>();
    }
}
