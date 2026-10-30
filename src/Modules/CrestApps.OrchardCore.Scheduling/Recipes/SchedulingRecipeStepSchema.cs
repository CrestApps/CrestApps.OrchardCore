using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Scheduling.Recipes;

/// <summary>
/// Describes the Scheduling recipe step to CrestApps Recipes.
/// </summary>
internal sealed class SchedulingRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => SchedulingConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(SchedulingConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'Scheduling'.")),
                ("Resources", SchedulingRecipeSchemas.Array(SchedulingRecipeSchemas.Resource(), "The staff, rooms and equipment to create or replace.")),
                ("AppointmentTypes", SchedulingRecipeSchemas.Array(SchedulingRecipeSchemas.AppointmentType(), "The appointment types to create or replace.")),
                ("Appointments", SchedulingRecipeSchemas.Array(SchedulingRecipeSchemas.Appointment(), "The appointments to create or replace. Import the Calendars step with its events first, so their calendar events exist.")),
                ("Waitlist", SchedulingRecipeSchemas.Array(SchedulingRecipeSchemas.WaitlistEntry(), "The waitlist entries to create or replace.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
