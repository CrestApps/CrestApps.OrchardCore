using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.BusinessDocuments.Deployments;

internal sealed class BusinessDocumentTemplatesDeploymentSource : CatalogDeploymentSource<BusinessDocumentTemplatesDeploymentStep, BusinessDocumentTemplate>
{
    public BusinessDocumentTemplatesDeploymentSource(IBusinessDocumentTemplateManager manager)
        : base(BusinessDocumentTemplatesDeploymentStep.EntriesName, manager)
    {
    }
}
