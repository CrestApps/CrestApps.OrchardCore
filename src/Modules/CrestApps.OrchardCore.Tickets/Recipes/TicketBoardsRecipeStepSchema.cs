using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// The schema of the <c>{TicketsConstants.RecipeSteps.Boards}</c> recipe step.
/// </summary>
internal sealed class TicketBoardsRecipeStepSchema : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => TicketsConstants.RecipeSteps.Boards;

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                (TicketBoardsRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The board id; omit it to create one.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The name.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the board is for.")),
                            ("Columns", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .MinItems(1)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The column id, unique on the board; omit it to have one made.")),
                                        ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The column's name.")),
                                        ("StatusIds", new JsonSchemaBuilder().Type(SchemaValueType.Array).MinItems(1).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The ids of the ticket statuses the column holds; a status is in one column of a board only.")),
                                        ("DefaultStatusId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The status a card dropped on the column gets when it has several; empty asks which one.")),
                                        ("Limit", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(1).Description("The most tickets the column should hold; the header shows when it holds more.")))
                                    .Required("Name", "StatusIds")
                                    .AdditionalProperties(true))
                                .Description("The columns, from left to right.")),
                            ("Query", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true).Description("Which tickets it shows: DeskIds, TeamIds, ContentTypes, PriorityIds, Assignee, Search, Requester, AccountId, DueWithin and Sort. The columns decide the statuses.")),
                            ("OwnerId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user who keeps it; empty for a shared board. Prefer OwnerUserName, since user ids differ from site to site.")),
                            ("OwnerUserName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user name of the user who keeps a personal board.")),
                            ("SharedWithTeamId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The team it is shared with.")),
                            ("Order", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Where it is listed.")),
                            ("CreatedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When it was created, in UTC.")),
                            ("ModifiedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When it last changed, in UTC.")),
                            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("More values of the board, by name.")))
                        .Required("Name", "Columns")
                        .AdditionalProperties(true))
                    .Description("The boards to create or update by id.")),
            ],
            [TicketBoardsRecipeStep.EntriesName]);
}
