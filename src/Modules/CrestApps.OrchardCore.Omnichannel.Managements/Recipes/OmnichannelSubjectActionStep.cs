using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Recipes;

/// <summary>
/// Imports the actions a subject disposition triggers, creating entries that do not exist and updating those that do.
/// </summary>
internal sealed class OmnichannelSubjectActionStep : NamedRecipeStepHandler
{
    private readonly ISourceCatalogManager<SubjectAction> _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectActionStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the subject actions.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public OmnichannelSubjectActionStep(
        ISourceCatalogManager<SubjectAction> manager,
        IStringLocalizer<OmnichannelSubjectActionStep> stringLocalizer)
        : base(OmnichannelDeploymentSteps.SubjectAction)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<OmnichannelSubjectActionStepModel>();
        var tokens = model.SubjectActions?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            SubjectAction entry = null;
            var isNew = false;

            var source = token[nameof(SubjectAction.Source)]?.GetValue<string>();

            if (string.IsNullOrEmpty(source))
            {
                context.Errors.Add(S["A subject action cannot be imported without a source."]);

                continue;
            }

            var id = token[nameof(SubjectAction.ItemId)]?.GetValue<string>();
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
                entry = await _manager.NewAsync(source, token);

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

    private sealed class OmnichannelSubjectActionStepModel
    {
        public JsonArray SubjectActions { get; set; }
    }
}
