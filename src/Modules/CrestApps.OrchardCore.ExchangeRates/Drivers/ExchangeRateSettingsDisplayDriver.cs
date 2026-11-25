using CrestApps.OrchardCore.ExchangeRates.Core;
using CrestApps.OrchardCore.ExchangeRates.Core.Services;
using CrestApps.OrchardCore.ExchangeRates.Models;
using CrestApps.OrchardCore.ExchangeRates.Services;
using CrestApps.OrchardCore.ExchangeRates.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ExchangeRates.Drivers;

/// <summary>
/// Edits the exchange rate settings under <b>Settings &gt; Commerce &gt; Exchange Rates</b>.
/// </summary>
public sealed class ExchangeRateSettingsDisplayDriver : SiteDisplayDriver<ExchangeRateSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IExchangeRateProvider> _providers;

    internal readonly IStringLocalizer S;

    public ExchangeRateSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<IExchangeRateProvider> providers,
        IStringLocalizer<ExchangeRateSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _providers = providers;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => ExchangeRatesConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, ExchangeRateSettings settings, BuildEditorContext context)
        => Initialize<ExchangeRateSettingsViewModel>("ExchangeRateSettings_Edit", model =>
        {
            model.BaseCurrency = settings.BaseCurrency;
            model.Currencies = string.Join(", ", settings.Currencies ?? []);
            model.ProviderName = settings.ProviderName;
            model.Providers = [.. _providers.Select(provider => new SelectListItem(provider.DisplayName, provider.Name))];
        }).Location(l => l.Zone("Content", "1").Card(S["Exchange Rates"], "10"))
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ExchangeRatesPermissions.ManageExchangeRates))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, ExchangeRateSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ExchangeRatesPermissions.ManageExchangeRates))
        {
            return null;
        }

        var model = new ExchangeRateSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var baseCurrency = ExchangeRateCodes.Normalize(model.BaseCurrency);
        var currencies = (model.Currencies ?? string.Empty)
            .Split([',', ' ', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ExchangeRateCodes.Normalize)
            .Distinct()
            .ToList();

        if (baseCurrency is not null && !ExchangeRateCodes.IsValid(baseCurrency))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseCurrency), S["The currency must be a three-letter code, such as USD."]);
        }

        if (currencies.Any(code => !ExchangeRateCodes.IsValid(code)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Currencies), S["List three-letter codes separated by commas, such as EUR, GBP."]);
        }

        if (!string.IsNullOrEmpty(model.ProviderName) && !_providers.Any(provider => provider.Name == model.ProviderName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProviderName), S["Choose one of the providers listed."]);
        }

        settings.BaseCurrency = baseCurrency;
        settings.Currencies = [.. currencies.Where(code => code != baseCurrency)];
        settings.ProviderName = string.IsNullOrEmpty(model.ProviderName) ? null : model.ProviderName;

        return Edit(site, settings, context);
    }
}
