using System.Globalization;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Edits <see cref="InstallmentPlanSettings"/> on the <b>Settings → Installment Plans</b> page.
/// </summary>
/// <remarks>
/// The settings have a page of their own rather than a section of the Subscriptions settings, because installment
/// plans no longer need the Subscriptions feature: a site that only lets customers pay balances in installments still
/// has somewhere to configure them.
/// </remarks>
public sealed class InstallmentPlanSettingsDisplayDriver : SiteDisplayDriver<InstallmentPlanSettings>
{
    private const int MaxRetries = 10;
    private const int MaxCheckoutCounts = 6;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly TransactionSourceOptions _sourceOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlanSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="sourceOptions">The registered transaction sources, offered for installments at checkout.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InstallmentPlanSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptions<TransactionSourceOptions> sourceOptions,
        IStringLocalizer<InstallmentPlanSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _sourceOptions = sourceOptions.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => SubscriptionConstants.InstallmentPlans.SettingsGroupId;

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(ISite site, InstallmentPlanSettings settings, BuildEditorContext context)
    {
        if (!await CanManageAsync())
        {
            return null;
        }

        return Initialize<InstallmentPlanSettingsViewModel>("InstallmentPlanSettings_Edit", model =>
        {
            model.RetryDays = string.Join(", ", (settings.RetryDays ?? []).Select(days => days.ToString(CultureInfo.InvariantCulture)));
            model.DefaultCollectionMethod = settings.DefaultCollectionMethod;
            model.CheckoutInstallmentCounts = string.Join(", ", (settings.CheckoutInstallmentCounts ?? []).Select(count => count.ToString(CultureInfo.InvariantCulture)));
            model.CheckoutFrequency = settings.CheckoutFrequency;
            model.CheckoutMinimumAmount = settings.CheckoutMinimumAmount;
            model.CheckoutSources = [.. (settings.CheckoutSources ?? [])];
            model.AvailableSources = BuildSourceItems(settings.CheckoutSources ?? []);
        }).Location("Content:20")
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, InstallmentPlanSettings settings, UpdateEditorContext context)
    {
        if (!await CanManageAsync())
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

        if (TryParseCheckoutCounts(model.CheckoutInstallmentCounts, out var counts))
        {
            settings.CheckoutInstallmentCounts = counts;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CheckoutInstallmentCounts), S["Enter up to {0} whole numbers of payments between 2 and 120, separated by commas, or leave it empty.", MaxCheckoutCounts]);
        }

        if (Enum.IsDefined(model.CheckoutFrequency))
        {
            settings.CheckoutFrequency = model.CheckoutFrequency;
        }

        if (model.CheckoutMinimumAmount < 0m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CheckoutMinimumAmount), S["The smallest balance can't be negative."]);
        }
        else
        {
            settings.CheckoutMinimumAmount = model.CheckoutMinimumAmount;
        }

        // Only sources that are registered can be chosen; one posted by a stale or hand-built form is dropped.
        settings.CheckoutSources = [.. (model.CheckoutSources ?? [])
            .Where(source => !string.IsNullOrEmpty(source) && _sourceOptions.Sources.ContainsKey(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return await EditAsync(site, settings, context);
    }

    /// <summary>
    /// Reads the numbers of payments a customer may choose at checkout: up to six whole numbers between 2 and 120.
    /// One payment is not offered, because it is the same as paying in full.
    /// </summary>
    /// <param name="value">What the administrator typed.</param>
    /// <param name="counts">The numbers, in ascending order, without repeats.</param>
    internal static bool TryParseCheckoutCounts(string value, out int[] counts)
    {
        counts = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var parts = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length > MaxCheckoutCounts)
        {
            return false;
        }

        var parsed = new SortedSet<int>();

        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 2 || count > 120)
            {
                return false;
            }

            parsed.Add(count);
        }

        counts = [.. parsed];

        return true;
    }

    private List<SelectListItem> BuildSourceItems(string[] selected)
        => [.. _sourceOptions.Sources.Values
            .Where(source => !string.Equals(source.Name, SubscriptionConstants.InstallmentPlans.TransactionSource, StringComparison.OrdinalIgnoreCase))
            .OrderBy(source => source.DisplayName?.Value ?? source.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(source => new SelectListItem(
                source.DisplayName?.Value ?? source.Name,
                source.Name,
                selected.Contains(source.Name, StringComparer.OrdinalIgnoreCase)))];

    // Whoever manages installment plans manages their settings.
    private async Task<bool> CanManageAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user is not null &&
            await _authorizationService.AuthorizeAsync(user, SubscriptionPermissions.ManageInstallmentPlans);
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
