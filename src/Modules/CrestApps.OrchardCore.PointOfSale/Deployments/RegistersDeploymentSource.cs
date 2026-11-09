using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Recipes;
using CrestApps.OrchardCore.PointOfSale.Services;

namespace CrestApps.OrchardCore.PointOfSale.Deployments;

/// <summary>
/// Writes every register into a <c>Registers</c> recipe step, keyed by its id and its name.
/// </summary>
internal sealed class RegistersDeploymentSource : CatalogStoreDeploymentSource<RegistersDeploymentStep, Register>
{
    public RegistersDeploymentSource(IRegisterStore store)
        : base(RegistersStep.EntriesName, store)
    {
    }

    protected override IEnumerable<Register> Order(IEnumerable<Register> entries)
        => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
}
