using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Dispatch.Recipes;

/// <summary>
/// Describes the <c>DispatchSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class DispatchSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(DispatchSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How visits are scheduled and dispatched: the objective, arrival windows, travel, customer notices and visits at risk.")
            .Properties(
                (nameof(DispatchSettings.CreateVisitsForNewWorkOrders), Flag("Whether a new work order gets an unscheduled visit. Defaults to true.")),
                (nameof(DispatchSettings.ArrivalWindowMinutes), Integer("The length of the arrival window promised to the customer, in minutes. Defaults to 120.")),
                (nameof(DispatchSettings.HorizonDays), Integer("How many days ahead candidates are looked for. Defaults to 7.")),
                (nameof(DispatchSettings.BufferMinutes), Integer("The minutes kept free between two visits on top of the travel. Defaults to 10.")),
                (nameof(DispatchSettings.StrictTerritory), Flag("Whether a worker must belong to the visit's territory.")),
                (nameof(DispatchSettings.TimeZoneId), Text("The IANA time zone the board shows; the site's when empty.")),
                (nameof(DispatchSettings.Objective), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("The weights candidates are ranked by.")
                    .Properties(
                        (nameof(DispatchObjective.TravelWeight), Integer("The weight of a short trip to the visit. Defaults to 5.")),
                        (nameof(DispatchObjective.UrgencyWeight), Integer("The weight of starting soon, before the work order is due. Defaults to 3.")),
                        (nameof(DispatchObjective.ProficiencyWeight), Integer("The weight of a skill level above the one needed. Defaults to 2.")),
                        (nameof(DispatchObjective.PreferredWeight), Integer("The weight of a worker the work order prefers. Defaults to 4.")),
                        (nameof(DispatchObjective.BalanceWeight), Integer("The weight of a worker with fewer visits that day. Defaults to 2.")))
                    .AdditionalProperties(true)),
                (nameof(DispatchSettings.AtRiskMinutes), Integer("How many minutes late a visit is at risk: running over, or not started. Defaults to 15.")),
                (nameof(DispatchSettings.OptimizerSeconds), Integer("How long the optimizer looks for a better plan, in seconds. Defaults to 5.")),
                (nameof(DispatchSettings.NoticesEnabled), Flag("Whether customers are told about their visit.")),
                (nameof(DispatchSettings.TrackingEnabled), Flag("Whether notices carry a link to follow the technician. Defaults to true.")),
                (nameof(DispatchSettings.ReminderHour), Integer("The local hour the reminders go out the day before, 0 to 23. Defaults to 17.")),
                (nameof(DispatchSettings.ScheduledText), Text("The notice when a visit is scheduled or moved, with placeholders such as {Company}, {Number}, {Date} and {Window}.")),
                (nameof(DispatchSettings.ReminderText), Text("The reminder the day before, with placeholders such as {Company}, {Date} and {Window}.")),
                (nameof(DispatchSettings.EnRouteText), Text("The notice when the technician is on the way, with placeholders such as {Company}, {Technician}, {Eta} and {TrackingUrl}.")),
                (nameof(DispatchSettings.CompletedText), Text("The notice when the visit is done, with placeholders such as {Company} and {Number}.")),
                (nameof(DispatchSettings.CanceledText), Text("The notice when the visit is canceled, with placeholders such as {Company}, {Number} and {Date}.")),
                (nameof(DispatchSettings.BookingWorkOrderTypeId), Text("The id of the work order type booked when a customer chooses an arrival window through Scheduling.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder Flag(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);

    private static JsonSchemaBuilder Integer(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description(description);
}
