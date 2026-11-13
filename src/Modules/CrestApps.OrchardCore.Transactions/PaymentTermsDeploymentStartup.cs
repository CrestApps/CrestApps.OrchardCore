using CrestApps.OrchardCore.Transactions.Deployments;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Payment terms travel with deployment plans.
/// </summary>
[Feature(TransactionsConstants.Features.PaymentTerms)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class PaymentTermsDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<PaymentTermsDeploymentSource, PaymentTermsDeploymentStep>();
    }
}
