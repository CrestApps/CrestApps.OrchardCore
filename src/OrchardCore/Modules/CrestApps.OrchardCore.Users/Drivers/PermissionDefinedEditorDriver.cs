using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.Contents;
using OrchardCore.Contents.Models;
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.Views;
using USR = OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Drivers;

public sealed class PermissionDefinedEditorDriver : ContentPartDisplayDriver<CommonPart>
{
    public static string PermissionDefinedEditor => "PermissionDefinedEditor";

    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<USR.IUser> _userManager;

    internal readonly IStringLocalizer S;

    public PermissionDefinedEditorDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        UserManager<USR.IUser> userManager,
        IStringLocalizer<PermissionDefinedEditorDriver> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(CommonPart part, BuildPartEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.EditContent))
        {
            return null;
        }

        var settings = context.TypePartDefinition.GetSettings<CommonPartSettings>();

        if (settings.DisplayOwnerEditor)
        {
            return Initialize<OwnerEditorViewModel>("CommonPart_Edit__Owner", async model =>
            {
                if (part.ContentItem.Owner != null)
                {
                    // TODO Move this editor to a user picker.
                    var user = await _userManager.FindByIdAsync(part.ContentItem.Owner);
                    model.OwnerName = user?.UserName ?? _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                }
            }).RenderWhen(async () => await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.PublishContent, part.ContentItem));
        }

        return null;
    }

    public override async Task<IDisplayResult> UpdateAsync(CommonPart part, UpdatePartEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.EditContent))
        {
            return null;
        }

        var settings = context.TypePartDefinition.GetSettings<CommonPartSettings>();

        if (!settings.DisplayOwnerEditor)
        {
            if (part.ContentItem.Owner == null)
            {
                part.ContentItem.Owner = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            }
        }
        else
        {
            var model = new OwnerEditorViewModel();

            if (part.ContentItem.Owner != null)
            {
                var user = await _userManager.FindByIdAsync(part.ContentItem.Owner);
                model.OwnerName = user?.UserName;
            }

            var priorOwnerName = model.OwnerName;
            await context.Updater.TryUpdateModelAsync(model, Prefix);

            if (model.OwnerName != priorOwnerName)
            {
                var newOwner = await _userManager.FindByNameAsync(model.OwnerName);

                if (newOwner == null)
                {
                    context.Updater.ModelState.AddModelError("CommonPart.OwnerName", S["Invalid user name"]);
                }
                else
                {
                    part.ContentItem.Owner = await _userManager.GetUserIdAsync(newOwner);
                }
            }
        }

        return await EditAsync(part, context);
    }
}
