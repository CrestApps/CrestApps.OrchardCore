using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Bookings.Recipes;

/// <summary>
/// Describes the <c>BookingDates</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class BookingDatesRecipeStepSchema : RecipeStepSchemaBase
{
    public override string Name => BookingDatesStep.RecipeStepName;

    protected override JsonSchema CreateSchema()
    {
        var date = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("A date of a booking product. A date the product has already is left as it is.")
            .Properties(
                ("ContentItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The content item id of the booking product.")),
                ("Date", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The date, as yyyy-MM-dd. Give Date or DaysFromToday.")),
                ("DaysFromToday", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The date as a number of days from today, so a sample's dates stay ahead.")),
                ("Time", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The start time of a time slot, as HH:mm. Leave it out for a whole day.")),
                ("Capacity", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The places the date has. 10 when left out.")),
                ("Price", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The date's own price, or leave it out for the product's.")))
            .Required("ContentItemId")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Adds dates to booking products, such as a tour's departures.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(BookingDatesStep.RecipeStepName).Description("Recipe step discriminator. Must be 'BookingDates'.")),
                ("Dates", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(date).Description("The dates to add.")))
            .Required("name", "Dates")
            .AdditionalProperties(true)
            .Build();
    }
}
