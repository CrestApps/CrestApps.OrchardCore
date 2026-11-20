using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// The schema of the <c>{TicketsConstants.RecipeSteps.Priorities}</c> recipe step.
/// </summary>
internal sealed class TicketPrioritiesRecipeStepSchema : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => TicketsConstants.RecipeSteps.Priorities;

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                (TicketPrioritiesRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The ticket priority id; omit it to create one.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The name.")),
                            ("Rank", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Its rank; higher is more urgent.")),
                            ("Color", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The badge color, a CSS color.")),
                            ("CreatedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When it was created, in UTC.")),
                            ("ModifiedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When it last changed, in UTC.")),
                            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("More values of the ticket priority, by name.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The ticket priorities to create or update by id.")),
            ],
            [TicketPrioritiesRecipeStep.EntriesName]);
}
