using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Display driver for the fields shared by every AI tool instance regardless of its source. The unique
/// name and the model-facing description are always rendered at the very top of the editor, above any
/// source specific fields.
/// </summary>
internal sealed class AIToolInstanceDisplayDriver : DisplayDriver<AIToolInstance>
{
    private readonly INamedCatalog<AIToolInstance> _instancesCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceDisplayDriver"/> class.
    /// </summary>
    /// <param name="instancesCatalog">The tool instances catalog used to enforce unique names.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolInstanceDisplayDriver(
        INamedCatalog<AIToolInstance> instancesCatalog,
        IStringLocalizer<AIToolInstanceDisplayDriver> stringLocalizer)
    {
        _instancesCatalog = instancesCatalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIToolInstance instance, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = instance.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("AIToolInstance_Checkbox_SummaryAdmin", instance)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("AIToolInstance_Fields_SummaryAdmin", instance)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AIToolInstance_Description_SummaryAdmin", instance)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(instance.Description))),
            View("AIToolInstance_Buttons_SummaryAdmin", instance).Location("Actions:5"),
            View("AIToolInstance_DefaultTags_SummaryAdmin", instance)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("AIToolInstance_DefaultMeta_SummaryAdmin", instance)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5")
        );
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        return Initialize<AIToolInstanceFieldsViewModel>("AIToolInstanceFields_Edit", model =>
        {
            model.Name = instance.Name;
            model.Description = instance.Description;
            model.IsNew = context.IsNew;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        var model = new AIToolInstanceFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            var name = model.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is required."]);
            }
            else if (await _instancesCatalog.FindByNameAsync(name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Another tool instance with the same name exists."]);
            }

            instance.Name = name;
        }

        if (string.IsNullOrWhiteSpace(model.Description))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Description), S["Description is required so the AI model can tell instances apart."]);
        }

        instance.Description = model.Description?.Trim();

        return Edit(instance, context);
    }
}
