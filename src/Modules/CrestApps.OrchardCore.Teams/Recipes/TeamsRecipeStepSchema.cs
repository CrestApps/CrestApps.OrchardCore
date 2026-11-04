using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Teams.Recipes;

/// <summary>
/// Describes the Teams recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TeamsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => TeamsConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TeamsConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'Teams'.")),
                ("Teams", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(Team()).Description("The teams to create, or update by id or name, in any order.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
    {
        return new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);
    }

    private static JsonSchemaBuilder Team()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("itemId", Text("The team id; omit to match by name.")),
                ("name", Text("The team name.")),
                ("kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Organization", "Region", "Site", "Department", "Team", "Crew")),
                ("parentName", Text("The name of the team it belongs to, which may be listed after it.")),
                ("parentId", Text("The id of the team it belongs to.")),
                ("description", Text("What the team does.")),
                ("linkedRoleName", Text("A role whose members are treated as part of the team.")),
                ("timeZoneId", Text("The team's IANA time zone.")),
                ("enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the team is in use.")))
            .Required("name")
            .AdditionalProperties(true);
    }
}
