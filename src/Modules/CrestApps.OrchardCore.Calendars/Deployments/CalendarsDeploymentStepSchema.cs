using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Calendars.Deployments;

/// <summary>
/// Describes the Calendars deployment step's settings to CrestApps Recipes.
/// </summary>
internal sealed class CalendarsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(CalendarsDeploymentStep);

    protected override string DisplayText => "Calendars";

    protected override string Description => "Exports every calendar, optionally with its events and everyone's categories.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeEvents", DeploymentSchemaBuilders.Boolean("When true, exports the events of each calendar too, up to 10,000 per calendar."));
        yield return ("IncludeCategories", DeploymentSchemaBuilders.Boolean("When true (the default), exports everyone's categories too, so the events keep their colors."));
    }
}
