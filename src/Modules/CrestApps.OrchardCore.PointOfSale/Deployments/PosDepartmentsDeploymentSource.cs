using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Recipes;
using CrestApps.OrchardCore.PointOfSale.Services;

namespace CrestApps.OrchardCore.PointOfSale.Deployments;

/// <summary>
/// Writes every department into a <c>PosDepartments</c> recipe step, keyed by its id and its name.
/// </summary>
internal sealed class PosDepartmentsDeploymentSource : CatalogStoreDeploymentSource<PosDepartmentsDeploymentStep, PosDepartment>
{
    public PosDepartmentsDeploymentSource(IPosDepartmentStore store)
        : base(PosDepartmentsStep.EntriesName, store)
    {
    }

    protected override IEnumerable<PosDepartment> Order(IEnumerable<PosDepartment> entries)
        => entries.OrderBy(entry => entry.Position).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
}
