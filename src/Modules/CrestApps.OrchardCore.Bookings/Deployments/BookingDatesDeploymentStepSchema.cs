using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Bookings.Deployments;

/// <summary>
/// Describes the booking dates deployment step to CrestApps Recipes.
/// </summary>
internal sealed class BookingDatesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(BookingDatesDeploymentStep);

    protected override string DisplayText => "Booking dates";

    protected override string Description => "Exports the booking products' upcoming open dates with their capacity and price.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
