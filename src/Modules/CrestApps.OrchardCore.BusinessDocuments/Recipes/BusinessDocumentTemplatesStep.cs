using CrestApps.OrchardCore.BusinessDocuments.Deployments;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.BusinessDocuments.Recipes;

internal sealed class BusinessDocumentTemplatesStep : CatalogRecipeStep<BusinessDocumentTemplate>
{
    public BusinessDocumentTemplatesStep(IBusinessDocumentTemplateManager manager)
        : base(BusinessDocumentTemplatesDeploymentStep.StepName, BusinessDocumentTemplatesDeploymentStep.EntriesName, manager)
    {
    }
}
