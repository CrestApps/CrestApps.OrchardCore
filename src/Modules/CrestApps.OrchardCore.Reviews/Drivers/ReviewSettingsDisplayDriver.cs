using CrestApps.OrchardCore.Reviews.Models;
using CrestApps.OrchardCore.Reviews.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Reviews.Drivers;

public sealed class ReviewSettingsDisplayDriver : SiteDisplayDriver<ReviewSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public ReviewSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<ReviewSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => ReviewsConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, ReviewSettings settings, BuildEditorContext context)
    {
        return Combine(
            Initialize<ReviewSettingsViewModel>("ReviewSettings_Edit", model => Fill(model, settings))
                .Location(l => l.Zone("Content", "1").Card(S["Moderation"], "10"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ReviewsPermissions.ManageReviews))
                .OnGroup(SettingsGroupId),
            Initialize<ReviewSettingsViewModel>("ReviewSettingsRequests_Edit", model => Fill(model, settings))
                .Location(l => l.Zone("Content", "2").Card(S["Review Requests"], "20"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ReviewsPermissions.ManageReviews))
                .OnGroup(SettingsGroupId));
    }

    private static void Fill(ReviewSettingsViewModel model, ReviewSettings settings)
    {
        model.RequireApproval = settings.RequireApproval;
        model.BuyersOnly = settings.BuyersOnly;
        model.SendReviewRequests = settings.SendReviewRequests;
        model.ReviewRequestDelayDays = settings.ReviewRequestDelayDays;
    }

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
        settings.SendReviewRequests = model.SendReviewRequests;
        settings.ReviewRequestDelayDays = Math.Clamp(model.ReviewRequestDelayDays, 0, 60);

        return Edit(site, settings, context);
    }
}
