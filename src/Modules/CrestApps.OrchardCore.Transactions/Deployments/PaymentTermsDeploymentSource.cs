using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Transactions.Deployments;

/// <summary>
/// Writes every payment term to the <see cref="PaymentTermsDeploymentStep"/> of a deployment plan.
/// </summary>
internal sealed class PaymentTermsDeploymentSource : CatalogDeploymentSource<PaymentTermsDeploymentStep, PaymentTerm>
{
    public PaymentTermsDeploymentSource(IPaymentTermManager manager)
        : base("Terms", manager)
    {
    }
}
