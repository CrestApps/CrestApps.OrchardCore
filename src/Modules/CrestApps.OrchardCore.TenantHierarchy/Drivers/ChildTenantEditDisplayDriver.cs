using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.TenantHierarchy.Drivers;

/// <summary>
/// Builds the editor of a child tenant in cards: its name, address and description, then the setup recipe when it is
/// created or its details when it is edited.
/// </summary>
internal sealed class ChildTenantEditDisplayDriver : DisplayDriver<ChildTenantEditViewModel>
{
    internal readonly IStringLocalizer S;

    public ChildTenantEditDisplayDriver(IStringLocalizer<ChildTenantEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChildTenantEditViewModel model, BuildEditorContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<ChildTenantEditViewModel>("ChildTenantGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
        };

        if (model.IsNew)
        {
            results.Add(Initialize<ChildTenantEditViewModel>("ChildTenantRecipe_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Setup Recipe"], "20")));
        }
        else
        {
            results.Add(Initialize<ChildTenantEditViewModel>("ChildTenantDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Details"], "20")));
        }

        return Combine([.. results]);
    }

    public override async Task<IDisplayResult> UpdateAsync(ChildTenantEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.DisplayName, m => m.Slug, m => m.Description, m => m.RecipeName);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(ChildTenantEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ChildTenantEditViewModel source, ChildTenantEditViewModel model)
    {
        model.EntryId = source.EntryId;
        model.DisplayName = source.DisplayName;
        model.Slug = source.Slug;
        model.Description = source.Description;
        model.RecipeName = source.RecipeName;
        model.Recipes = source.Recipes;
        model.RecipeDescriptions = source.RecipeDescriptions;
        model.HostPattern = source.HostPattern;
        model.Scheme = source.Scheme;
        model.Info = source.Info;
        model.Labels = source.Labels;
        model.TotalCount = source.TotalCount;
        model.MaxChildren = source.MaxChildren;
    }
}