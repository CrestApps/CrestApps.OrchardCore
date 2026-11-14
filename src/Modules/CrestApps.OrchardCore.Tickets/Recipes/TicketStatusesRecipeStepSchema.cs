using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// The schema of the <c>{TicketsConstants.RecipeSteps.Statuses}</c> recipe step.
/// </summary>
internal sealed class TicketStatusesRecipeStepSchema : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => TicketsConstants.RecipeSteps.Statuses;

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                (TicketStatusesRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The ticket status id; omit it to create one.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The name.")),
                            ("Category", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("New", "Open", "Pending", "OnHold", "Solved", "Closed").Description("What the status means.")),
                            ("CustomerLabel", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What customers see instead of the name.")),
                            ("PausesTimers", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether service-level timers stop while a ticket is in the status.")),
                            ("Color", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The badge color, a CSS color.")),
                            ("Order", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Where it is listed; lower comes first.")),
                            ("CreatedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When it was created, in UTC.")),
                            ("ModifiedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When it last changed, in UTC.")),
                            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("More values of the ticket status, by name.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The ticket statuses to create or update by id.")),
            ],
            [TicketStatusesRecipeStep.EntriesName]);
}
