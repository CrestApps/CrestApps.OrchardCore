using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Core.Models;
using CrestApps.OrchardCore.Users.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Cache;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Users.Drivers;

public sealed class DisplayNameSettingsDisplayDriver : SiteDisplayDriver<DisplayNameSettings>
{
    public const string GroupId = "userDisplayName";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ITagCache _tagCache;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public DisplayNameSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ILiquidTemplateManager liquidTemplateManager,
        ITagCache tagCache,
        IHtmlLocalizer<DisplayNameSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<DisplayNameSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _liquidTemplateManager = liquidTemplateManager;
        _tagCache = tagCache;

        H = htmlLocalizer;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, DisplayNameSettings settings, BuildEditorContext context)
    {
        return Initialize<EditDisplayNameSettingPartViewModel>("DisplayNameSettings_Edit", model =>
        {
            model.Type = settings.Type;
            model.Template = settings.Template;
            model.DisplayName = settings.DisplayName;
            model.FirstName = settings.FirstName;
            model.LastName = settings.LastName;
            model.MiddleName = settings.MiddleName;
            model.Types =
            [
                new SelectListItem(S["Username"], nameof(DisplayNameType.Username)),
                new SelectListItem(S["Display Name"], nameof(DisplayNameType.DisplayName)),
                new SelectListItem(S["First Middle Last name"], nameof(DisplayNameType.FirstThenLast)),
                new SelectListItem(S["Last, First Middle name"], nameof(DisplayNameType.LastThenFirst)),
                new SelectListItem(S["Custom format"], nameof(DisplayNameType.Other)),

            ];
            model.PropertyTypes =
            [
                new SelectListItem(S["Don't use"], nameof(DisplayNamePropertyType.None)),
                new SelectListItem(S["Optional"],nameof( DisplayNamePropertyType.Optional)),
                new SelectListItem(S["Required"], nameof(DisplayNamePropertyType.Required)),
            ];
        }).Location("Content:5")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, UserPermissions.ManageDisplaySettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, DisplayNameSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, UserPermissions.ManageDisplaySettings))
        {
            return null;
        }

        var model = new EditDisplayNameSettingPartViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.DisplayName = model.DisplayName;
        settings.FirstName = model.FirstName;
        settings.LastName = model.LastName;
        settings.MiddleName = model.MiddleName;

        if (model.Type == DisplayNameType.Other)
        {
            if (string.IsNullOrWhiteSpace(model.Template))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Template), S["Template is required."]);
            }
            else if (!_liquidTemplateManager.Validate(model.Template, out var templateErrors))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Template), string.Join(' ', templateErrors));
            }

            settings.Template = model.Template;
        }

        if (settings.Type != model.Type ||
            model.Type == DisplayNameType.Other && settings.Template != model.Template)
        {
            settings.Type = model.Type;

            if (context.Updater.ModelState.IsValid)
            {
                await _tagCache.RemoveTagAsync(UsersConstants.UserDisplayNameCacheTag);
            }
        }

        return Edit(site, settings, context);
    }
}
