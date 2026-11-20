using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// The schema of the <c>{TicketsConstants.RecipeSteps.Views}</c> recipe step.
/// </summary>
internal sealed class TicketViewsRecipeStepSchema : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => TicketsConstants.RecipeSteps.Views;

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                (TicketViewsRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The ticket view id; omit it to create one.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The name.")),
                            ("OwnerId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user who made it; empty for a shared view.")),
                            ("SharedWithTeamId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The team it is shared with.")),
                            ("Order", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Where it is listed.")),
                            ("Query", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true).Description("Which tickets it lists: Search, DeskIds, Categories, StatusIds, PriorityIds, TeamIds, Assignee, ContentTypes, Requester, AccountId, DueWithin, Sort, Skip and Take.")),
                            ("CreatedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When it was created, in UTC.")),
                            ("ModifiedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When it last changed, in UTC.")),
                            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("More values of the ticket view, by name.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The saved views to create or update by id.")),
            ],
            [TicketViewsRecipeStep.EntriesName]);
}
