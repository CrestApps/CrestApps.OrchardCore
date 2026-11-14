using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The Payroll card on Pay Setup's pay code editor, for people who manage payroll settings: how the code is calculated
/// and taxed, its limits and its rules, kept in the code's <see cref="PayrollPayCodeSettings"/>.
/// </summary>
internal sealed class PayrollPayCodeDisplayDriver : DisplayDriver<PayCode>
{
    private const int BlankYearLimitRows = 1;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPayCodeManager _codes;
    private readonly PayrollTaxCategoryOptions _categories;
    private readonly PayrollFormBoxOptions _formBoxes;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public PayrollPayCodeDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IPayCodeManager codes,
        IOptions<PayrollTaxCategoryOptions> categories,
        IOptions<PayrollFormBoxOptions> formBoxes,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<PayrollPayCodeDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _codes = codes;
        _categories = categories.Value;
        _formBoxes = formBoxes.Value;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(PayCode code, BuildEditorContext context)
        => Card(code, null);

    public override async Task<IDisplayResult> UpdateAsync(PayCode code, UpdateEditorContext context)
    {
        if (!await CanManageAsync())
        {
            return null;
        }

        var model = new PayrollPayCodeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = Read(code, model, context);

        code.Put(settings);

        return Card(code, model);
    }

    /// <summary>
    /// Reads the card: numbers with a dot for decimals, the rules a class allows, dated yearly limits.
    /// </summary>
    /// <param name="code">The pay code, with its class already set.</param>
    /// <param name="model">The posted card.</param>
    /// <param name="context">The editor context, for errors; or <see langword="null"/>.</param>
    /// <returns>The settings.</returns>
    internal PayrollPayCodeSettings Read(PayCode code, PayrollPayCodeViewModel model, UpdateEditorContext context)
    {
        var settings = new PayrollPayCodeSettings
        {
            Calculation = Enum.TryParse<PayCodeCalculation>(model.Calculation, out var calculation) ? calculation : PayCodeCalculation.FixedAmount,
            Supplemental = model.Supplemental,
            CountsInRegularRate = model.CountsInRegularRate,
            CountsAsHoursWorked = model.CountsAsHoursWorked,
            Priority = model.Priority,
            Arrears = Enum.TryParse<PayCodeArrearsRule>(model.Arrears, out var arrears) ? arrears : PayCodeArrearsRule.None,
            Proration = Enum.TryParse<PayCodeProrationRule>(model.Proration, out var proration) ? proration : PayCodeProrationRule.None,
            InRetroPay = model.InRetroPay,
            PeriodLimit = Amount(model.PeriodLimit, nameof(model.PeriodLimit), context),
            LifetimeLimit = Amount(model.LifetimeLimit, nameof(model.LifetimeLimit), context),
        };

        if (settings.Calculation == PayCodeCalculation.PercentOfEarnings)
        {
            settings.BaseCodeIds = [.. (model.BaseCodeIds ?? []).Where(id => !string.IsNullOrEmpty(id) && id != code.ItemId).Distinct(StringComparer.Ordinal)];
        }

        if (settings.Calculation == PayCodeCalculation.PercentOfDisposableEarnings && code.Class != PayCodeClass.Deduction)
        {
            context?.Updater.ModelState.AddModelError(Prefix, nameof(model.Calculation), S["Only a deduction is a percent of disposable earnings."]);
        }

        if (settings.Arrears == PayCodeArrearsRule.CarryForward && code.Class != PayCodeClass.Deduction)
        {
            context?.Updater.ModelState.AddModelError(Prefix, nameof(model.Arrears), S["Only a deduction can be owed and taken later."]);
        }

        if (settings.Priority is < 0 or > 10000)
        {
            context?.Updater.ModelState.AddModelError(Prefix, nameof(model.Priority), S["The priority is a whole number from 0 to 10000."]);
        }

        var categories = _categories.Categories.Select(category => category.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var (category, value) in model.Treatments ?? [])
        {
            // A category no module registers any more keeps its stored treatment.
            if (Enum.TryParse<PayCodeTaxTreatment>(value, out var treatment) && (categories.Contains(category) || code.TreatmentFor(category) == treatment))
            {
                settings.TaxTreatment[category] = treatment;
            }
        }

        foreach (var row in model.YearLimits ?? [])
        {
            var amount = Amount(row.Amount, nameof(model.YearLimits), context);

            if (row.EffectiveFrom is null && amount is null)
            {
                continue;
            }

            if (row.EffectiveFrom is not { } from || amount is not { } value)
            {
                context?.Updater.ModelState.AddModelError(Prefix, nameof(model.YearLimits), S["A yearly limit needs its first day and its amount."]);

                continue;
            }

            if (settings.YearLimits.Any(limit => limit.EffectiveFrom == from))
            {
                context?.Updater.ModelState.AddModelError(Prefix, nameof(model.YearLimits), S["Two yearly limits start on {0}.", from.ToString("d", CultureInfo.CurrentCulture)]);

                continue;
            }

            settings.YearLimits.Add(new DatedAmount { EffectiveFrom = from, Amount = value });
        }

        settings.YearLimits = [.. settings.YearLimits.OrderBy(limit => limit.EffectiveFrom)];

        var boxes = _formBoxes.Boxes.Select(box => box.Name).ToHashSet(StringComparer.Ordinal);
        var stored = code.GetPayrollSettings().FormBoxes;

        settings.FormBoxes = [.. (model.FormBoxes ?? []).Where(box => boxes.Contains(box) || stored.Contains(box)).Distinct(StringComparer.Ordinal)];

        return settings;
    }

    private ShapeResult Card(PayCode code, PayrollPayCodeViewModel typed)
        => Initialize<PayrollPayCodeViewModel>("PayrollPayCode_Edit", async model =>
        {
            var settings = code.GetPayrollSettings();

            model.Calculation = typed?.Calculation ?? settings.Calculation.ToString();
            model.BaseCodeIds = typed?.BaseCodeIds ?? [.. settings.BaseCodeIds];
            model.Supplemental = typed?.Supplemental ?? settings.Supplemental;
            model.CountsInRegularRate = typed?.CountsInRegularRate ?? settings.CountsInRegularRate;
            model.CountsAsHoursWorked = typed?.CountsAsHoursWorked ?? settings.CountsAsHoursWorked;
            model.PeriodLimit = typed?.PeriodLimit ?? Text(settings.PeriodLimit);
            model.LifetimeLimit = typed?.LifetimeLimit ?? Text(settings.LifetimeLimit);
            model.Priority = typed?.Priority ?? settings.Priority;
            model.Arrears = typed?.Arrears ?? settings.Arrears.ToString();
            model.Proration = typed?.Proration ?? settings.Proration.ToString();
            model.InRetroPay = typed?.InRetroPay ?? settings.InRetroPay;
            model.FormBoxes = typed?.FormBoxes ?? [.. settings.FormBoxes];
            model.Treatments = typed?.Treatments ?? settings.TaxTreatment.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
            model.YearLimits = typed?.YearLimits ?? [.. settings.YearLimits.Select(limit => new DatedAmountViewModel { EffectiveFrom = limit.EffectiveFrom, Amount = Text(limit.Amount) })];

            for (var i = 0; i < BlankYearLimitRows; i++)
            {
                model.YearLimits.Add(new DatedAmountViewModel());
            }

            model.ClassDefault = PayrollPayCodeSettingsExtensions.DefaultTreatment(code.Class).ToString();
            model.Categories = [.. _categories.Categories.Select(category => new SelectListItem(_localizerFactory.Localize(category.DisplayName).Value, category.Name))];
            model.FormBoxOptions = [.. _formBoxes.Boxes.Select(box => new SelectListItem(_localizerFactory.Localize(box.DisplayName).Value, box.Name, model.FormBoxes.Contains(box.Name)))];
            model.BaseCodes = [.. (await _codes.GetAllAsync())
                .Where(other => other.ItemId != code.ItemId && other.Class == PayCodeClass.Earning)
                .OrderBy(other => other.Code, StringComparer.OrdinalIgnoreCase)
                .Select(other => new SelectListItem($"{other.Code} · {other.Name}", other.ItemId, model.BaseCodeIds.Contains(other.ItemId)))];
        }).Location("Content:6")
        .RenderWhen(() => CanManageAsync());

    private decimal? Amount(string text, string field, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            context?.Updater.ModelState.AddModelError(Prefix, field, S["A limit is an amount of zero or more, with a dot for decimals."]);

            return null;
        }

        return value;
    }

    private static string Text(decimal? value)
        => value?.ToString("0.##########", CultureInfo.InvariantCulture);

    private async Task<bool> CanManageAsync()
        => _httpContextAccessor.HttpContext?.User is { } user && await _authorizationService.AuthorizeAsync(user, PayrollPermissions.ManagePayrollSettings);
}
