using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;

/// <summary>
/// Imports messaging templates carried by a recipe step, creating entries that do not exist and updating those that do. An entry
/// is matched by its identifier alone, and a created entry keeps the identifier it was exported with so references to
/// it from other entries and steps keep resolving.
/// </summary>
internal sealed class OmnichannelMessageTemplateStep : NamedRecipeStepHandler
{
    private readonly IMessageTemplateManager _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelMessageTemplateStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the messaging templates.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public OmnichannelMessageTemplateStep(
        IMessageTemplateManager manager,
        IStringLocalizer<OmnichannelMessageTemplateStep> stringLocalizer)
        : base(MessagingDeploymentSteps.MessageTemplate)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<OmnichannelMessageTemplateStepModel>();
        var tokens = model.Templates?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            MessageTemplate entry = null;
            var isNew = false;

            var id = token[nameof(MessageTemplate.ItemId)]?.GetValue<string>();
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

    private sealed class OmnichannelMessageTemplateStepModel
    {
        public JsonArray Templates { get; set; }
    }
}
