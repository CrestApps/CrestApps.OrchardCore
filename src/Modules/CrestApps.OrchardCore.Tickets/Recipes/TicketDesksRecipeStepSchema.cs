using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// The schema of the <c>{TicketsConstants.RecipeSteps.Desks}</c> recipe step.
/// </summary>
internal sealed class TicketDesksRecipeStepSchema : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => TicketsConstants.RecipeSteps.Desks;

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                (TicketDesksRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The desk id; omit it to create one.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The name.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the desk is for.")),
                            ("Audience", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Customers", "Internal").Description("Who the desk serves.")),
                            ("DefaultTeamId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The team new tickets go to.")),
                            ("HoursScheduleId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The business hours schedule; empty is around the clock.")),
                            ("ServiceLevelPolicyId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The service-level policy of its tickets.")),
                            ("TicketTypes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The ticket types it offers; empty offers every type.")),
                            ("ReopenWindowDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Days after solving in which a requester reply reopens a ticket.")),
                            ("AutoCloseDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Days a solved ticket waits before it closes; 0 never.")),
                            ("AssignmentStrategy", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("How a new ticket with a team and no assignee is given to a member: LeastLoaded, RoundRobin, or empty to leave it for an agent.")),
                            ("RatingRequestDelayHours", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("Hours after a ticket is solved its requester is asked to rate it, with Tickets Portal on; 0 never asks.")),
                            ("AvailabilitySources", new JsonSchemaBuilder().Type(SchemaValueType.Array | SchemaValueType.Null).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The team availability sources assignment honors on the desk (WorkforceShifts, CalendarAbsences, ContactCenterPresence); omit for the defaults, which leave out Contact Center presence.")),
                            ("PriorityMatrix", new JsonSchemaBuilder().Type(SchemaValueType.Array | SchemaValueType.Null).Items(new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    ("Impact", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Low", "Medium", "High").Description("The impact.")),
                                    ("Urgency", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Low", "Medium", "High").Description("The urgency.")),
                                    ("PriorityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The priority a ticket with this impact and urgency gets.")))
                                .Required("Impact", "Urgency", "PriorityId")).Description("The priority of a ticket for each impact and urgency.")),
                            ("NotifyRequester", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether requesters are e-mailed.")),
                            ("NotifyAssignee", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether assignees are told.")),
                            ("CreatedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When it was created, in UTC.")),
                            ("ModifiedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When it last changed, in UTC.")),
                            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("More values of the desk, by name.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The desks to create or update by id.")),
            ],
            [TicketDesksRecipeStep.EntriesName]);
}
