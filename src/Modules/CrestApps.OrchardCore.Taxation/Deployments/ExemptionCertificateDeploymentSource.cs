using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Deployments;

/// <summary>
/// Writes every exemption certificate into an <c>ExemptionCertificate</c> recipe step, keyed by its id and by its customer and number.
/// </summary>
internal sealed class ExemptionCertificateDeploymentSource : CatalogStoreDeploymentSource<ExemptionCertificateDeploymentStep, ExemptionCertificate>
{
    public ExemptionCertificateDeploymentSource(IExemptionCertificateStore store)
        : base(ExemptionCertificateStep.EntriesName, store)
    {
    }

    protected override IEnumerable<ExemptionCertificate> Order(IEnumerable<ExemptionCertificate> entries)
        => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
}
