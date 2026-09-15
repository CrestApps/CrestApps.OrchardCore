using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports Contact Center business-hours calendars carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class ContactCenterBusinessHoursCalendarStep : NamedRecipeStepHandler
{
    private readonly IBusinessHoursCalendarManager _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterBusinessHoursCalendarStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the business-hours calendars.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public ContactCenterBusinessHoursCalendarStep(
        IBusinessHoursCalendarManager manager,
        IStringLocalizer<ContactCenterBusinessHoursCalendarStep> stringLocalizer)
        : base(ContactCenterDeploymentSteps.BusinessHoursCalendar)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<ContactCenterBusinessHoursCalendarStepModel>();
        var tokens = model.Calendars?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            BusinessHoursCalendar entry = null;
            var isNew = false;

            var id = token[nameof(BusinessHoursCalendar.ItemId)]?.GetValue<string>();
            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                entry = await _manager.FindByIdAsync(id);
            }

            if (entry is not null)
            {
                await _manager.UpdateAsync(entry, token);
            }
            else
            {
                isNew = true;
                entry = await _manager.NewAsync(token);

                if (hasId && UniqueId.IsValid(id))
                {
                    entry.ItemId = id;
                }
            }

            var validationResult = await _manager.ValidateAsync(entry);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            if (isNew)
            {
                await _manager.CreateAsync(entry);
            }
        }
    }

    private sealed class ContactCenterBusinessHoursCalendarStepModel
    {
        public JsonArray Calendars { get; set; }
    }
}
