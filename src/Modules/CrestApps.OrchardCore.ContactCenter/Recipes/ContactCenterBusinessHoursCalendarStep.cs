using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Legacy;
using CrestApps.OrchardCore.Core.Recipes;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Reads the <c>ContactCenterBusinessHoursCalendar</c> step of older recipes: each calendar becomes a Business Hours
/// schedule, with the calendar's id when it is a valid id, so the queues and entry points of the same recipe still name
/// it. New recipes use the <c>BusinessHoursSchedules</c> step.
/// </summary>
internal sealed class ContactCenterBusinessHoursCalendarStep : NamedRecipeStepHandler
{
    private readonly BusinessHoursCalendarMove _move;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterBusinessHoursCalendarStep"/> class.
    /// </summary>
    /// <param name="move">Turns calendars into Business Hours schedules.</param>
    public ContactCenterBusinessHoursCalendarStep(BusinessHoursCalendarMove move)
        : base(ContactCenterDeploymentSteps.BusinessHoursCalendar)
    {
        _move = move;
    }

    /// <inheritdoc/>
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var node in CatalogRecipeSections.Section(context.Step, "Calendars"))
        {
            var calendar = node.Deserialize<LegacyBusinessHoursCalendar>(JOptions.Default);

            if (calendar is null)
            {
                continue;
            }

            var (_, errors) = await _move.ImportAsync(calendar);

            if (errors.Count > 0)
            {
                context.Errors.Add($"A business hours calendar in the {context.Name} step was refused: {string.Join(" ", errors)}");
            }
        }
    }
}
