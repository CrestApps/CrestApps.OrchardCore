using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.TenantHierarchy.Drivers;

/// <summary>
/// Builds the editor of a parent tenant policy in cards: the parent itself, its child tenants, how child tenants are
/// opened and the words shown on screen.
/// </summary>
internal sealed class ParentPolicyEditDisplayDriver : DisplayDriver<ParentPolicyEditViewModel>
{
    internal readonly IStringLocalizer S;

    public ParentPolicyEditDisplayDriver(IStringLocalizer<ParentPolicyEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ParentPolicyEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<ParentPolicyEditViewModel>("ParentPolicyParent_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Parent"], "10")),
            Initialize<ParentPolicyEditViewModel>("ParentPolicyChildren_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Child Tenants"], "20")),
            Initialize<ParentPolicyEditViewModel>("ParentPolicyAccess_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Opening Child Tenants"], "30")),
            Initialize<ParentPolicyEditViewModel>("ParentPolicyLabels_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Words on Screen"], "40")));
    }

    public override async Task<IDisplayResult> UpdateAsync(ParentPolicyEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.TenantName,
            m => m.Slug,
            m => m.DisplayName,
            m => m.MaxChildren,
            m => m.ChildHostPattern,
            m => m.DatabaseStrategy,
            m => m.DatabasePool,
            m => m.Recipes,
            m => m.BlockedFeatures,
            m => m.DeniedLocalPermissions,
            m => m.RemovalGraceDays,
            m => m.RequireMfa,
            m => m.SessionValidationMinutes,
            m => m.SessionIdleMinutes,
            m => m.SessionLifetimeHours,
            m => m.SwitcherMode,
            m => m.ParentLabel,
            m => m.ChildLabel,
            m => m.ChildrenLabel);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the script of the page selects.
    protected override void BuildPrefix(ParentPolicyEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ParentPolicyEditViewModel source, ParentPolicyEditViewModel model)
    {
        model.TenantName = source.TenantName;
        model.Slug = source.Slug;
        model.DisplayName = source.DisplayName;
        model.MaxChildren = source.MaxChildren;
        model.ChildHostPattern = source.ChildHostPattern;
        model.DatabaseStrategy = source.DatabaseStrategy;
        model.DatabasePool = source.DatabasePool;
        model.Recipes = source.Recipes;
        model.BlockedFeatures = source.BlockedFeatures;
        model.DeniedLocalPermissions = source.DeniedLocalPermissions;
        model.RequireMfa = source.RequireMfa;
        model.SessionValidationMinutes = source.SessionValidationMinutes;
        model.SessionIdleMinutes = source.SessionIdleMinutes;
        model.SessionLifetimeHours = source.SessionLifetimeHours;
        model.SwitcherMode = source.SwitcherMode;
        model.ParentLabel = source.ParentLabel;
        model.ChildLabel = source.ChildLabel;
        model.ChildrenLabel = source.ChildrenLabel;
        model.RemovalGraceDays = source.RemovalGraceDays;
        model.IsNew = source.IsNew;
        model.PlatformDomain = source.PlatformDomain;
        model.Host = source.Host;
        model.Candidates = source.Candidates;
        model.Suggestions = source.Suggestions;
        model.AvailableRecipes = source.AvailableRecipes;
        model.DatabasePools = source.DatabasePools;
    }
}