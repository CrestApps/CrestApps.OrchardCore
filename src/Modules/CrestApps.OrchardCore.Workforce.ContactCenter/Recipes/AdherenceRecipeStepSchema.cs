using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Recipes;

/// <summary>
/// Describes the WorkforceAdherence recipe step to CrestApps Recipes.
/// </summary>
internal sealed class AdherenceRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => AdherenceRecipeStep.AdherenceStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Creates or updates adherence rule sets, matched by id or name.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(AdherenceRecipeStep.AdherenceStepName).Description("Recipe step discriminator. Must be 'WorkforceAdherence'.")),
                ("RuleSets", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Rule sets: name, teamNames and rules. Each rule has a kind (an activity kind such as Work or Break) or an activityName, allowedStatuses (agent states such as Available, Busy, WrapUp or Break), allowedReasons, startGraceMinutes, endGraceMinutes, minimumOutMinutes and alertAfterMinutes.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
