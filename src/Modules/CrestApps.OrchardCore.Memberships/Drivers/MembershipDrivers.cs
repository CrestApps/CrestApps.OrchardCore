using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Memberships.Drivers;

/// <summary>
/// The Membership part's editor: the roles a member is in, and for how long.
/// </summary>
internal sealed class MembershipPartDisplayDriver : ContentPartDisplayDriver<MembershipPart>
{
    // Everyone is in these already, and no purchase should make anyone an administrator.
    private static readonly string[] _excludedRoles = [OrchardCoreConstants.Roles.Anonymous, OrchardCoreConstants.Roles.Authenticated, OrchardCoreConstants.Roles.Administrator];

    private readonly IRoleService _roleService;

    internal readonly IStringLocalizer S;

    public MembershipPartDisplayDriver(IRoleService roleService, IStringLocalizer<MembershipPartDisplayDriver> stringLocalizer)
    {
        _roleService = roleService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(MembershipPart part, BuildPartEditorContext context)
        => Initialize<MembershipPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.RoleNames = part.RoleNames ?? [];
            model.DurationDays = part.DurationDays;
            model.AvailableRoles = await GetRolesAsync();
        });

    public override async Task<IDisplayResult> UpdateAsync(MembershipPart part, UpdatePartEditorContext context)
    {
        var model = new MembershipPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var available = await GetRolesAsync();

        part.RoleNames = [.. (model.RoleNames ?? []).Where(role => available.Contains(role, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase)];
        part.DurationDays = Math.Clamp(model.DurationDays, 0, 3650);

        if (part.RoleNames.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RoleNames), S["Choose at least one role for members."]);
        }

        return Edit(part, context);
    }

    private async Task<IReadOnlyList<string>> GetRolesAsync()
        => [.. (await _roleService.GetRoleNamesAsync()).Where(role => !_excludedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase)];
}

/// <summary>
/// Renders a membership as a row of the memberships admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class MembershipDisplayDriver : DisplayDriver<Membership>
{
    public override IDisplayResult Display(Membership membership, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = membership.UserName?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("Membership_Fields_SummaryAdmin", membership)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Membership_Description_SummaryAdmin", membership)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Ends:1"),
            View("Membership_Status_SummaryAdmin", membership)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("Membership_Buttons_SummaryAdmin", membership).Location("Actions:5"));
    }
}
