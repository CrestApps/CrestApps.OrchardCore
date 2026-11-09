using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.Bookings.Recipes;

/// <summary>
/// Describes <c>BookingPart</c>, the part that makes a product a booking, to CrestApps Recipes.
/// </summary>
public sealed class BookingPartSchemaDefinition : PartSchemaDefinitionBase
{
    public override string Name => nameof(BookingPart);

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("What is booked, how many can be on each date, how it's priced for the people in it, and what paying and canceling it takes. Dates of a departure are added with the BookingDates step.")
            .Properties(
                (nameof(BookingPart.Kind), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String, SchemaValueType.Integer)
                    .Enum("Departure", "Nightly", "TimeSlot", 0, 1, 2)
                    .Description("What is booked: Departure (0) for dated departures such as a tour, Nightly (1) for stays, or TimeSlot (2) for time slots within opening hours.")),
                (nameof(BookingPart.Capacity), Integer("How many can be booked on a date: seats on a departure, rooms a night, or bookings in a time slot. Defaults to 10.")),
                (nameof(BookingPart.PricePerPerson), Boolean("Whether the price is per person, such as a tour's, rather than per unit, such as a room's.")),
                (nameof(BookingPart.Occupancies), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Description("What a unit can hold, such as a single or a double room. None means one person a unit.")
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (nameof(BookingOccupancy.Id), Text("The identifier of the option, kept on the cart line.")),
                            (nameof(BookingOccupancy.Label), Text("The name of the option, such as Double room.")),
                            (nameof(BookingOccupancy.People), Integer("How many people the option is for. Defaults to 1.")),
                            (nameof(BookingOccupancy.PriceAdjustment), Number("What the option adds to, or takes off, the price, such as a single supplement.")))
                        .AdditionalProperties(true))),
                (nameof(BookingPart.MinNights), Integer("For stays: the fewest nights. Defaults to 1.")),
                (nameof(BookingPart.MaxNights), Integer("For stays: the most nights. Defaults to 28.")),
                (nameof(BookingPart.WeekendPrice), new JsonSchemaBuilder().Type(SchemaValueType.Number, SchemaValueType.Null).Description("For stays: the price of a Friday or Saturday night, or null for the product's price.")),
                (nameof(BookingPart.Hours), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Description("For time slots: when slots are offered, by day of the week. A day that isn't listed is closed.")
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (nameof(BookingOpeningHours.Day), new JsonSchemaBuilder()
                                .Type(SchemaValueType.String, SchemaValueType.Integer)
                                .Description("The day of the week, by name (Sunday to Saturday) or number (0 for Sunday).")),
                            (nameof(BookingOpeningHours.Opens), Text("When the first slot starts, as HH:mm.")),
                            (nameof(BookingOpeningHours.Closes), Text("When the last slot ends, as HH:mm.")))
                        .AdditionalProperties(true))),
                (nameof(BookingPart.SlotMinutes), Integer("For time slots: how long a slot is, in minutes. Defaults to 60.")),
                (nameof(BookingPart.DaysAhead), Integer("How many days ahead stays and time slots can be booked. Defaults to 180.")),
                (nameof(BookingPart.DepositPercent), Number("The percent of the price a buyer can pay now as a deposit, below 100, or 0 to pay in full.")),
                (nameof(BookingPart.BalanceDueDaysBefore), Integer("How many days before the date the rest is due and charged. A booking closer than that is paid in full. Defaults to 30.")),
                (nameof(BookingPart.CancellationTiers), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Description("What a cancellation costs by how long before the date it is, such as 10% up to 60 days before and 50% up to 30. None means a full refund.")
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (nameof(BookingCancellationTier.DaysBefore), Integer("Up to how many days before the date the fee applies.")),
                            (nameof(BookingCancellationTier.FeePercent), Number("The fee, from 0 to 100 percent of the price.")))
                        .AdditionalProperties(true))),
                (nameof(BookingPart.RequestToBook), Boolean("Whether the store confirms each booking before the buyer pays; the buyer then gets a pay link.")),
                (nameof(BookingPart.AskForTravelers), Boolean("Whether the buyer gives each traveler's or attendee's details at checkout.")),
                (nameof(BookingPart.BlockingCalendarId), new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The calendar whose busy times close the product's dates and times, such as a room's calendar. Used by the Bookings Calendar feature.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder Integer(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description(description);

    private static JsonSchemaBuilder Number(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number).Description(description);

    private static JsonSchemaBuilder Boolean(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);
}
