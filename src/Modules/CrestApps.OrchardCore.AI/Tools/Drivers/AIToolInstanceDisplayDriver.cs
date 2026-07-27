using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
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
        return CombineAsync(
            View("AIToolInstance_Fields_SummaryAdmin", instance).Location("Content:1"),
            View("AIToolInstance_Buttons_SummaryAdmin", instance).Location("Actions:5"),
            View("AIToolInstance_DefaultTags_SummaryAdmin", instance).Location("Tags:5"),
            View("AIToolInstance_DefaultMeta_SummaryAdmin", instance).Location("Meta:5")
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
