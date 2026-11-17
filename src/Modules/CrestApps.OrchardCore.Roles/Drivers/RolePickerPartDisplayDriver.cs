using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Roles.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Roles.Drivers;

internal sealed class RolePickerPartDisplayDriver : ContentPartDisplayDriver<RolePickerPart>
{
    private readonly RoleManager<IRole> _roleManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolePickerPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="roleManager">The role manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RolePickerPartDisplayDriver(
        RoleManager<IRole> roleManager,
        IStringLocalizer<RolePickerPartDisplayDriver> stringLocalizer)
    {
        _roleManager = roleManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RolePickerPart part, BuildPartEditorContext context)
    {
        return Initialize<RolePickerViewModel>(GetEditorShapeType(context), m =>
        {
            var settings = context.TypePartDefinition.GetSettings<RolePickerPartSettings>();
            var labels = Describe(context.TypePartDefinition, S);

            m.DisplayName = labels.Label;
            m.Placeholder = labels.Placeholder;
            m.Hint = labels.Hint;
            m.Settings = settings;
            m.Roles = part.RoleNames;

            m.AvailableRoles = _roleManager.Roles
                .Select(role => role.RoleName)
                .Except(settings.ExcludedRoles ?? [])
                .Order()
                .Select(x => new SelectListItem(x, x))
                .ToArray();
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(RolePickerPart part, UpdatePartEditorContext context)
    {
        var model = new RolePickerViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = context.TypePartDefinition.GetSettings<RolePickerPartSettings>();

        var selectedRoles = (model.Roles ?? []).Except(settings.ExcludedRoles ?? []).ToArray();

        if (settings.Required && selectedRoles.Length == 0)
        {
            if (settings.AllowSelectMultiple)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Roles), S["You must select at least one role."]);
            }
            else
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Roles), S["You must select a role."]);
            }
        }

        if (!settings.AllowSelectMultiple && selectedRoles.Length > 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Roles), S["Only one roles is allowed."]);
        }

        part.RoleNames = selectedRoles;

        return Edit(part, context);
    }

    /// <summary>
    /// Describes the picker on a content type's editor. A picker that restricts who can see the content (Content Access
    /// Control's <see cref="RolePickerPartContentAccessControlSettings.IsContentRestricted"/>) says so, and with no role
    /// picked reads "Any role", since then anyone may see it; a name an administrator gave the part still wins for the
    /// label, and a hint they wrote for the hint. Any other picker keeps the part's name.
    /// </summary>
    /// <param name="typePart">The picker on the content type.</param>
    /// <param name="localizer">The string localizer.</param>
    /// <returns>The label, the text shown with no role picked, and the hint.</returns>
    internal static (string Label, string Placeholder, string Hint) Describe(ContentTypePartDefinition typePart, IStringLocalizer localizer)
    {
        var settings = typePart.GetSettings<RolePickerPartSettings>();
        var restricts = typePart.GetSettings<RolePickerPartContentAccessControlSettings>().IsContentRestricted;
        var ownName = typePart.GetSettings<ContentTypePartSettings>().DisplayName;

        if (!restricts)
        {
            var name = typePart.DisplayName();

            return (name, name, settings.Hint);
        }

        return (
            string.IsNullOrWhiteSpace(ownName) ? localizer["Which roles can see this content"] : ownName,
            localizer["Any role"],
            string.IsNullOrWhiteSpace(settings.Hint) ? localizer["Leave empty so everyone can see it."] : settings.Hint);
    }
}
