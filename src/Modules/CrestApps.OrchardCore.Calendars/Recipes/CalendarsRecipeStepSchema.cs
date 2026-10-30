using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Calendars.Recipes;

/// <summary>
/// Describes the Calendars recipe step to CrestApps Recipes: calendars with their events, and categories.
/// </summary>
internal sealed class CalendarsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => CalendarsConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(CalendarsConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'Calendars'.")),
                ("Categories", CalendarRecipeSchemas.Array(CalendarRecipeSchemas.Category(), "The categories to create or update, before the calendars.")),
                ("Calendars", CalendarRecipeSchemas.Array(CalendarRecipeSchemas.Calendar(), "The calendars to create or update, each with its events.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
