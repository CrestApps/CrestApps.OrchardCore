using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for the generic fields shared by all AI template sources:
/// Title, Technical Name, Description, Category, and IsListable.
/// </summary>
internal sealed class AIProfileTemplateDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly INamedCatalog<AIProfileTemplate> _templatesCatalog;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateDisplayDriver(
        INamedCatalog<AIProfileTemplate> templatesCatalog,
        IStringLocalizer<ProfileTemplateDisplayDriver> stringLocalizer)
    {
        _templatesCatalog = templatesCatalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProfileTemplate template, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIProfileTemplate_Fields_SummaryAdmin", template).Location("Content:1"),
            View("AIProfileTemplate_Buttons_SummaryAdmin", template).Location("Actions:5"),
            View("AIProfileTemplate_DefaultTags_SummaryAdmin", template).Location("Tags:5"),
            View("AIProfileTemplate_DefaultMeta_SummaryAdmin", template).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<AIProfileTemplateFieldsViewModel>("AIProfileTemplateFields_Edit", model =>
        {
            model.DisplayText = template.DisplayText;
            model.Name = template.Name;
            model.Description = template.Description;
            model.Category = template.Category;
            model.IsListable = template.IsListable;
            model.IsNew = context.IsNew;
        }).Location(template.Source == AITemplateSources.Profile ? "Content:1%General;1" : "Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        var model = new AIProfileTemplateFieldsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            if (string.IsNullOrEmpty(model.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is required."]);
            }
            else if (await _templatesCatalog.FindByNameAsync(model.Name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Another template with the same name exists."]);
            }

            template.Name = model.Name;
        }

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Title is required."]);
        }

        template.DisplayText = model.DisplayText;
        template.Description = model.Description;
        template.Category = model.Category;
        template.IsListable = model.IsListable;

        return Edit(template, context);
    }
}
