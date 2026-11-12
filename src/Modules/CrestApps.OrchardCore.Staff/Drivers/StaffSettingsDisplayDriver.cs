using CrestApps.OrchardCore.Staff.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Staff.Drivers;

/// <summary>
/// The Staff site settings page.
/// </summary>
public sealed class StaffSettingsDisplayDriver : SiteDisplayDriver<StaffSettings>
{
    /// <summary>
    /// The settings group.
    /// </summary>
    public const string GroupId = "staff";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public StaffSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc />
    protected override string SettingsGroupId => GroupId;

    /// <inheritdoc />
    public override IDisplayResult Edit(ISite site, StaffSettings settings, BuildEditorContext context)
        => Initialize<StaffSettingsViewModel>("StaffSettings_Edit", model => model.LimitPickersToStaff = settings.LimitPickersToStaff)
            .Location("Content:1")
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, StaffPermissions.ManageStaff))
            .OnGroup(SettingsGroupId);

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ISite site, StaffSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, StaffPermissions.ManageStaff))
        {
            return null;
        }

        var model = new StaffSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        settings.LimitPickersToStaff = model.LimitPickersToStaff;

        return Edit(site, settings, context);
    }
}

/// <summary>
/// The values of the Staff settings page.
/// </summary>
public class StaffSettingsViewModel
{
    /// <summary>
    /// Gets or sets whether people pickers offer only staff.
    /// </summary>
    public bool LimitPickersToStaff { get; set; }
}
