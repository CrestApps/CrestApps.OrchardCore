using System.Globalization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The rows of a profile's pay items and the pay item editor. Amounts and percents are typed with a dot for decimals.
/// </summary>
internal sealed class PayItemDisplayDriver : DisplayDriver<EmployeePayItem>
{
    private readonly IPayCodeManager _codes;

    internal readonly IStringLocalizer S;

    public PayItemDisplayDriver(IPayCodeManager codes, IStringLocalizer<PayItemDisplayDriver> stringLocalizer)
    {
        _codes = codes;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(EmployeePayItem item, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("EmployeePayItem_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("EmployeePayItem_Tags_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("EmployeePayItem_Buttons_SummaryAdmin", item).Location("Actions:5"),
            View("EmployeePayItem_ActionsMenu_SummaryAdmin", item).Location("ActionsMenu:10"));
    }

    public override async Task<IDisplayResult> EditAsync(EmployeePayItem item, BuildEditorContext context)
    {
        var codes = await _codes.GetAllAsync();

        return Initialize<PayItemViewModel>("PayItemFields_Edit", model =>
        {
            model.PayCodeId = item.PayCodeId;
            model.Amount = Text(item.Amount);
            model.Percent = Text(item.Percent);
            model.StartDate = context.IsNew && item.StartDate == default ? null : item.StartDate;
            model.EndDate = item.EndDate;
            model.EmployerMatchPercent = Text(item.EmployerMatchPercent);
            model.EmployerMatchLimitPercent = Text(item.EmployerMatchLimitPercent);
            model.Goal = Text(item.Goal);
            model.PeriodMaximum = Text(item.PeriodMaximum);
            model.YearMaximum = Text(item.YearMaximum);
            model.Note = item.Note;
            model.PaidTowardGoal = Text(item.PaidTowardGoal);
            model.ArrearsBalance = Text(item.ArrearsBalance);
            model.Codes = [.. codes
                .Where(code => code.Enabled || code.ItemId == item.PayCodeId)
                .OrderBy(code => code.Class)
                .ThenBy(code => code.Code, StringComparer.Ordinal)
                .Select(code => new SelectListItem($"{code.Code} · {code.Name} ({code.Class})", code.ItemId, code.ItemId == item.PayCodeId))];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(EmployeePayItem item, UpdateEditorContext context)
    {
        var model = new PayItemViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        item.PayCodeId = model.PayCodeId;
        item.Amount = Read(model.Amount, nameof(model.Amount), context);
        item.Percent = Read(model.Percent, nameof(model.Percent), context);
        item.EndDate = model.EndDate;
        item.EmployerMatchPercent = Read(model.EmployerMatchPercent, nameof(model.EmployerMatchPercent), context);
        item.EmployerMatchLimitPercent = Read(model.EmployerMatchLimitPercent, nameof(model.EmployerMatchLimitPercent), context);
        item.Goal = Read(model.Goal, nameof(model.Goal), context);
        item.PeriodMaximum = Read(model.PeriodMaximum, nameof(model.PeriodMaximum), context);
        item.YearMaximum = Read(model.YearMaximum, nameof(model.YearMaximum), context);
        item.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();

        if (model.StartDate is { } start)
        {
            item.StartDate = start;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.StartDate), S["Give the first day."]);
        }

        return await EditAsync(item, context);
    }

    private decimal? Read(string value, string field, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
        {
            return Math.Round(number, 4, MidpointRounding.AwayFromZero);
        }

        context.Updater.ModelState.AddModelError(Prefix, field, S["Type a number, with a dot for decimals."]);

        return null;
    }

    private static string Text(decimal? value)
        => value?.ToString("0.####", CultureInfo.InvariantCulture);
}
