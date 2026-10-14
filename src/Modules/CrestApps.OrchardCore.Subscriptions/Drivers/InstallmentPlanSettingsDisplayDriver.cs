using System.Globalization;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Edits <see cref="InstallmentPlanSettings"/> on the Subscriptions settings page.
/// </summary>
public sealed class InstallmentPlanSettingsDisplayDriver : SiteDisplayDriver<InstallmentPlanSettings>
{
    private const int MaxRetries = 10;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlanSettingsDisplayDriver"/> class.
    /// </summary>
    public InstallmentPlanSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<InstallmentPlanSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => SubscriptionSettingsDisplayDriver.GroupId;

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(ISite site, InstallmentPlanSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return null;
        }

        return Initialize<InstallmentPlanSettingsViewModel>("InstallmentPlanSettings_Edit", model =>
        {
            model.RetryDays = string.Join(", ", (settings.RetryDays ?? []).Select(days => days.ToString(CultureInfo.InvariantCulture)));
            model.DefaultCollectionMethod = settings.DefaultCollectionMethod;
        }).Location("Content:20")
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, InstallmentPlanSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return null;
        }

        var model = new InstallmentPlanSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (TryParseRetryDays(model.RetryDays, out var retryDays))
        {
            settings.RetryDays = retryDays;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RetryDays), S["Enter up to {0} whole numbers of days between 1 and 60, separated by commas, or leave it empty for no retries.", MaxRetries]);
        }

        if (Enum.IsDefined(model.DefaultCollectionMethod))
        {
            settings.DefaultCollectionMethod = model.DefaultCollectionMethod;
        }

        return await EditAsync(site, settings, context);
    }

    // The retry delays are a short list of day counts. Anything else is rejected rather than half-applied, because a
    // misread delay decides when a customer's card is charged again.
    internal static bool TryParseRetryDays(string value, out int[] retryDays)
    {
        retryDays = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var parts = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length > MaxRetries)
        {
            return false;
        }

        var days = new List<int>(parts.Length);

        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var day) || day < 1 || day > 60)
            {
                return false;
            }

            days.Add(day);
        }

        retryDays = [.. days];

        return true;
    }
}
