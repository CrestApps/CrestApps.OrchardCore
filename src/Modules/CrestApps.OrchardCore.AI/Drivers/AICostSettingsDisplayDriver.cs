using System.Globalization;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Edits what the AI and the phone lines cost, on the AI settings page, for the estimated cost of each AI call.
/// </summary>
public sealed class AICostSettingsDisplayDriver : SiteDisplayDriver<AICostSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <inheritdoc/>
    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICostSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AICostSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IStringLocalizer<AICostSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, AICostSettings settings, BuildEditorContext context)
    {
        return Initialize<AICostSettingsViewModel>("AICostSettings_Edit", model =>
        {
            model.Currency = settings.Currency;
            model.TelephonyPerMinute = settings.TelephonyPerMinute;
            model.SmsPerMessage = settings.SmsPerMessage;
            model.DeploymentPrices = FormatPrices(settings.Deployments);
        }).Location("Content:9%Costs;9")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, AICostSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new AICostSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var (prices, errors) = ParsePrices(model.DeploymentPrices);

        foreach (var error in errors)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DeploymentPrices), S["Line {0} is not 'deployment | input | output | audio input | audio output | per minute' with numbers.", error]);
        }

        settings.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency.Trim().ToUpperInvariant();
        settings.TelephonyPerMinute = Math.Max(0, model.TelephonyPerMinute);
        settings.SmsPerMessage = Math.Max(0, model.SmsPerMessage);
        settings.Deployments = prices;

        return Edit(site, settings, context);
    }

    /// <summary>
    /// Reads the price lines: <c>deployment | input | output | audio input | audio output | per minute</c>, prices per
    /// million tokens, trailing values optional.
    /// </summary>
    /// <param name="text">The lines.</param>
    /// <returns>The prices, and the numbers of the lines that could not be read.</returns>
    internal static (List<AIDeploymentPrice> Prices, List<int> Errors) ParsePrices(string text)
    {
        var prices = new List<AIDeploymentPrice>();
        var errors = new List<int>();
        var lines = (text ?? string.Empty).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            var values = new decimal[5];
            var valid = parts.Length >= 2 && parts[0].Length > 0;

            for (var i = 1; valid && i < parts.Length && i <= 5; i++)
            {
                valid = parts[i].Length == 0 || decimal.TryParse(parts[i], NumberStyles.Number, CultureInfo.InvariantCulture, out values[i - 1]);
            }

            if (!valid)
            {
                errors.Add(index + 1);

                continue;
            }

            prices.Add(new AIDeploymentPrice
            {
                DeploymentName = parts[0],
                InputPerMillion = values[0],
                OutputPerMillion = values[1],
                AudioInputPerMillion = values[2],
                AudioOutputPerMillion = values[3],
                PerMinute = values[4],
            });
        }

        return (prices, errors);
    }

    /// <summary>
    /// Writes the prices as lines.
    /// </summary>
    /// <param name="prices">The prices.</param>
    /// <returns>The lines.</returns>
    internal static string FormatPrices(IEnumerable<AIDeploymentPrice> prices)
        => string.Join('\n', (prices ?? []).Select(price => string.Join(" | ",
            price.DeploymentName,
            price.InputPerMillion.ToString(CultureInfo.InvariantCulture),
            price.OutputPerMillion.ToString(CultureInfo.InvariantCulture),
            price.AudioInputPerMillion.ToString(CultureInfo.InvariantCulture),
            price.AudioOutputPerMillion.ToString(CultureInfo.InvariantCulture),
            price.PerMinute.ToString(CultureInfo.InvariantCulture))));
}
