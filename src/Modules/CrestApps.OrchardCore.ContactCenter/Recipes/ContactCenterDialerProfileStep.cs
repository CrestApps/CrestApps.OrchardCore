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
/// Imports Contact Center dialer profiles carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class ContactCenterDialerProfileStep : NamedRecipeStepHandler
{
    private readonly IDialerProfileManager _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDialerProfileStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the dialer profiles.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public ContactCenterDialerProfileStep(
        IDialerProfileManager manager,
        IStringLocalizer<ContactCenterDialerProfileStep> stringLocalizer)
        : base(ContactCenterDeploymentSteps.DialerProfile)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<ContactCenterDialerProfileStepModel>();
        var tokens = model.DialerProfiles?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            DialerProfile entry = null;
            var isNew = false;

            var id = token[nameof(DialerProfile.ItemId)]?.GetValue<string>();
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

    private sealed class ContactCenterDialerProfileStepModel
    {
        public JsonArray DialerProfiles { get; set; }
    }
}
