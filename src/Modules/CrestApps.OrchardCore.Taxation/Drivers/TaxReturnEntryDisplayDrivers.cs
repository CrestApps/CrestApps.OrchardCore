using System.Globalization;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// The fields of the payment form of a tax return: the amount, the day, the reference and the method. A pack adds its own
/// fields with another driver of <see cref="TaxReturnPaymentEntry"/>.
/// </summary>
internal sealed class TaxReturnPaymentEntryDisplayDriver : DisplayDriver<TaxReturnPaymentEntry>
{
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public TaxReturnPaymentEntryDisplayDriver(
        ILocalClock localClock,
        IStringLocalizer<TaxReturnPaymentEntryDisplayDriver> stringLocalizer)
    {
        _localClock = localClock;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TaxReturnPaymentEntry entry, BuildEditorContext context)
        => Initialize<TaxReturnPaymentEntryViewModel>("TaxReturnPaymentEntryFields_Edit", async model =>
        {
            var payment = entry.Payment ?? new TaxReturnPayment();
            var balance = entry.Return?.Balance ?? 0;

            model.Amount = payment.Amount > 0
                ? payment.Amount.ToString(CultureInfo.InvariantCulture)
                : balance > 0
                    ? balance.ToString(CultureInfo.InvariantCulture)
                    : null;
            model.PaidOn = payment.PaidOn == default
                ? DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime)
                : payment.PaidOn;
            model.Reference = payment.Reference;
            model.Method = payment.Method;
        }).Location("Content:1");

    public override async Task<IDisplayResult> UpdateAsync(TaxReturnPaymentEntry entry, UpdateEditorContext context)
    {
        var model = new TaxReturnPaymentEntryViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        entry.Payment ??= new TaxReturnPayment();

        if (decimal.TryParse(model.Amount?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            entry.Payment.Amount = amount;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Amount), S["Enter the amount, with a dot for decimals."]);
        }

        entry.Payment.PaidOn = model.PaidOn ?? default;
        entry.Payment.Reference = model.Reference;
        entry.Payment.Method = model.Method;

        return Edit(entry, context);
    }
}

/// <summary>
/// The fields of the form that files a document electronically: the format and channel, and whether it is a test when a
/// channel offered takes tests. A pack adds its own choices with another driver of <see cref="EFileSubmissionRequest"/>.
/// </summary>
internal sealed class EFileSubmissionRequestDisplayDriver : DisplayDriver<EFileSubmissionRequest>
{
    private readonly IEFilingService _eFiling;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public EFileSubmissionRequestDisplayDriver(
        IEFilingService eFiling,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<EFileSubmissionRequestDisplayDriver> stringLocalizer)
    {
        _eFiling = eFiling;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(EFileSubmissionRequest request, BuildEditorContext context)
    {
        if (request.Target is null)
        {
            return null;
        }

        return Initialize<EFileSubmissionRequestViewModel>("EFileSubmissionRequestFields_Edit", model =>
        {
            var options = _eFiling.GetOptions(request.Target);

            model.Option = string.IsNullOrEmpty(request.Format) ? null : $"{request.Format}|{request.Transport}";
            model.IsTest = request.IsTest;
            model.Options =
            [
                .. options.Select(option => new SelectListItem(
                    $"{_localizerFactory.Localize(option.Format.DisplayName).Value} · {_localizerFactory.Localize(option.Transport.DisplayName).Value}",
                    $"{option.Format.Name}|{option.Transport.Name}")),
            ];
            model.TestChannels =
            [
                .. options
                    .Select(option => option.Transport)
                    .Where(transport => transport.SupportsTestSubmissions)
                    .DistinctBy(transport => transport.Name)
                    .Select(transport => _localizerFactory.Localize(transport.DisplayName).Value),
            ];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(EFileSubmissionRequest request, UpdateEditorContext context)
    {
        var model = new EFileSubmissionRequestViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var parts = (model.Option ?? string.Empty).Split('|', 2);

        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
        {
            request.Format = parts[0];
            request.Transport = parts[1];
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Option), S["Choose how to file the document."]);
        }

        request.IsTest = model.IsTest;

        return Edit(request, context);
    }
}
