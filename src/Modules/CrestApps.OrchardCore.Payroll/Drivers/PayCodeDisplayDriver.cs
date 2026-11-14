using System.Globalization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The rows of the pay code list and the pay code editor. Amounts are typed with the invariant culture.
/// </summary>
internal sealed class PayCodeDisplayDriver : DisplayDriver<PayCode>
{
    private readonly PayCodeKindOptions _kinds;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public PayCodeDisplayDriver(IOptions<PayCodeKindOptions> kinds, IStringLocalizerFactory localizerFactory, IStringLocalizer<PayCodeDisplayDriver> stringLocalizer)
    {
        _kinds = kinds.Value;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(PayCode code, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{code.Code} {code.Name}".ToLowerInvariant();
        }

        return CombineAsync(
            View("PayCode_Fields_SummaryAdmin", code)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PayCode_Tags_SummaryAdmin", code)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("PayCode_Buttons_SummaryAdmin", code).Location("Actions:5"),
            View("PayCode_ActionsMenu_SummaryAdmin", code).Location("ActionsMenu:10"));
    }

    public override IDisplayResult Edit(PayCode code, BuildEditorContext context)
    {
        return Initialize<PayCodeViewModel>("PayCodeFields_Edit", model =>
        {
            model.Code = code.Code;
            model.Name = code.Name;
            model.Class = code.Class.ToString();
            model.Kind = code.Kind;
            model.Multiplier = code.Multiplier.ToString("0.####", CultureInfo.InvariantCulture);
            model.PremiumPerHour = code.PremiumPerHour?.ToString("0.####", CultureInfo.InvariantCulture);
            model.ExportCode = code.ExportCode;
            model.Enabled = code.Enabled;
            model.KindOptions = KindOptions(code.Kind);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(PayCode code, UpdateEditorContext context)
    {
        var model = new PayCodeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        code.Code = model.Code;
        code.Name = model.Name;
        code.Class = Enum.TryParse<PayCodeClass>(model.Class, out var payClass) ? payClass : PayCodeClass.Earning;
        code.Kind = string.IsNullOrWhiteSpace(model.Kind) ? PayCodeKinds.Regular : model.Kind.Trim();
        code.ExportCode = model.ExportCode;
        code.Enabled = model.Enabled;

        if (Parse(model.Multiplier) is { } multiplier)
        {
            code.Multiplier = multiplier;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Multiplier), S["Enter the multiplier as a number, such as 1.5."]);
        }

        if (string.IsNullOrWhiteSpace(model.PremiumPerHour))
        {
            code.PremiumPerHour = null;
        }
        else if (Parse(model.PremiumPerHour) is { } premium)
        {
            code.PremiumPerHour = premium;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PremiumPerHour), S["Enter the premium as a number, such as 2.50, or leave it empty."]);
        }

        return Edit(code, context);
    }

    private List<SelectListItem> KindOptions(string selected)
    {
        var options = new List<SelectListItem>
        {
            new(S["Regular hours"], PayCodeKinds.Regular),
            new(S["Overtime"], PayCodeKinds.Overtime),
            new(S["Double time"], PayCodeKinds.DoubleTime),
            new(S["Holiday hours"], PayCodeKinds.Holiday),
            new(S["Weekend hours"], PayCodeKinds.Weekend),
            new(S["Paid time off"], PayCodeKinds.PaidTimeOff),
            new(S["Predictability pay"], PayCodeKinds.PredictabilityPay),
            new(S["Call back"], PayCodeKinds.CallBack),
            new(S["A shift differential (a premium for hours in a window)"], PayCodeKinds.ShiftDifferential),
            new(S["Unpaid time"], PayCodeKinds.Unpaid),
            new(S["Salary"], PayCodeKinds.Salary),
            new(S["Bonus"], PayCodeKinds.Bonus),
            new(S["Commission"], PayCodeKinds.Commission),
            new(S["Tips"], PayCodeKinds.Tips),
        };

        foreach (var (name, displayName) in _kinds.Kinds)
        {
            if (options.All(option => option.Value != name))
            {
                options.Add(new SelectListItem(_localizerFactory.Localize(displayName).Value, name));
            }
        }

        // A kind no module registers any more stays choosable on the code that has it.
        if (!string.IsNullOrEmpty(selected) && options.All(option => option.Value != selected))
        {
            options.Add(new SelectListItem(selected, selected));
        }

        foreach (var option in options)
        {
            option.Selected = option.Value == selected;
        }

        return options;
    }

    private static decimal? Parse(string value)
        => !string.IsNullOrWhiteSpace(value) && decimal.TryParse(value.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            ? Math.Round(number, 4, MidpointRounding.AwayFromZero)
            : null;
}
