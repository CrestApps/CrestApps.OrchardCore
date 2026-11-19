using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Transactions.Deployments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Transactions.Recipes;

/// <summary>
/// Creates or updates the payment terms a recipe lists under <c>Terms</c>, matched by id.
/// </summary>
internal sealed class PaymentTermsStep : CatalogRecipeStep<PaymentTerm>
{
    public PaymentTermsStep(IPaymentTermManager manager)
        : base(PaymentTermsDeploymentStep.StepName, "Terms", manager)
    {
    }
}
