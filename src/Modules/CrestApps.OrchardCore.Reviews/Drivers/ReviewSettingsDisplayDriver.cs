using CrestApps.OrchardCore.Reviews.Models;
using CrestApps.OrchardCore.Reviews.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Reviews.Drivers;

public sealed class ReviewSettingsDisplayDriver : SiteDisplayDriver<ReviewSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ReviewSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => ReviewsConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, ReviewSettings settings, BuildEditorContext context)
        => Initialize<ReviewSettingsViewModel>("ReviewSettings_Edit", model =>
        {
            model.RequireApproval = settings.RequireApproval;
            model.BuyersOnly = settings.BuyersOnly;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ReviewsPermissions.ManageReviews))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ReviewSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ReviewsPermissions.ManageReviews))
        {
            return null;
        }

        var model = new ReviewSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.RequireApproval = model.RequireApproval;
        settings.BuyersOnly = model.BuyersOnly;

        return Edit(site, settings, context);
    }
}
