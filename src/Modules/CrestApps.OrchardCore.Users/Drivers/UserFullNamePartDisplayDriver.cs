using CrestApps.OrchardCore.Users.Core.ViewModels;
using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Drivers;

public sealed class UserFullNamePartDisplayDriver : SectionDisplayDriver<User, UserFullNamePart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public UserFullNamePartDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<UserFullNamePartDisplayDriver> stringLocalizer,
        ISiteService siteService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
        _siteService = siteService;
    }

    public override Task<IDisplayResult> DisplayAsync(User user, UserFullNamePart section, BuildDisplayContext context)
    {
        var result = Initialize<UserFullNamePartViewModel>("UserFullNamePart", async vm =>
        {
            vm.FirstName = section?.FirstName;
            vm.MiddleName = section?.MiddleName;
            vm.LastName = section?.LastName;

            vm.User = user;
            vm.Settings = (await _siteService.GetSiteSettingsAsync()).As<DisplayNameSettings>();
        }).Location("SummaryAdmin", "Header:1.5");

        return Task.FromResult<IDisplayResult>(result);
    }

    public override Task<IDisplayResult> EditAsync(User user, UserFullNamePart part, BuildEditorContext context)
    {
        var result = Initialize<UserFullNamePartViewModel>("UserFullNamePart_Edit", async model =>
        {
            model.FirstName = part?.FirstName;
            model.MiddleName = part?.MiddleName;
            model.LastName = part?.LastName;
            model.DisplayName = part?.DisplayName;

            model.User = user;
            model.Settings = (await _siteService.GetSiteSettingsAsync()).As<DisplayNameSettings>();
        }).Location("Content:1.5")
          .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.EditUsers, user));

        return Task.FromResult<IDisplayResult>(result);
    }

    public override async Task<IDisplayResult> UpdateAsync(User user, UserFullNamePart part, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.EditUsers, user))
        {
            // When the user is only editing their profile never update this part of the user.
            return null;
        }

        var model = new UserFullNamePartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = (await _siteService.GetSiteSettingsAsync()).As<DisplayNameSettings>();

        if (settings.DisplayName == DisplayNamePropertyType.Required && string.IsNullOrWhiteSpace(model.DisplayName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayName), S["Display name is a required value."]);
        }

        if (settings.FirstName == DisplayNamePropertyType.Required && string.IsNullOrWhiteSpace(model.FirstName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.FirstName), S["First name is a required value."]);
        }

        if (settings.LastName == DisplayNamePropertyType.Required && string.IsNullOrWhiteSpace(model.LastName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.LastName), S["Last name is a required value."]);
        }

        if (settings.MiddleName == DisplayNamePropertyType.Required && string.IsNullOrWhiteSpace(model.MiddleName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MiddleName), S["Middle name is a required value."]);
        }

        part.DisplayName = model.DisplayName;
        part.FirstName = model.FirstName;
        part.LastName = model.LastName;
        part.MiddleName = model.MiddleName;

        return await EditAsync(user, part, context);
    }
}
