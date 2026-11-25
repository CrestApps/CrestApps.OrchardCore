using System.Globalization;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Banking.Checks.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Checks.Drivers;

internal sealed class CheckLayoutDisplayDriver : DisplayDriver<CheckLayout>
{
    private const long MaxImageBytes = 1024 * 1024;
    private const long MaxFontBytes = 2 * 1024 * 1024;

    private readonly ICheckFileStore _files;
    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IStringLocalizer S;

    public CheckLayoutDisplayDriver(ICheckFileStore files, IAddressEditorService addressEditor, IShapeFactory shapeFactory, IStringLocalizer<CheckLayoutDisplayDriver> stringLocalizer)
    {
        _files = files;
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(CheckLayout layout, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = layout.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("CheckLayout_Fields_SummaryAdmin", layout)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("CheckLayout_Tags_SummaryAdmin", layout)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("CheckLayout_Buttons_SummaryAdmin", layout).Location("Actions:5"),
            View("CheckLayout_ActionsMenu_SummaryAdmin", layout).Location("ActionsMenu:10"));
    }

    public override IDisplayResult Edit(CheckLayout layout, BuildEditorContext context)
        => Editor(layout, null);

    // The editor, with the payer's address as stored, or as posted when it is given.
    private CombinedResult Editor(CheckLayout layout, AddressEditorViewModel postedPayerAddress)
        => Combine(
            Initialize<CheckLayoutViewModel>("CheckLayoutFields_Edit", model => Populate(model, layout))
                .Location(l => l.Zone("Content", "1").Card(S["The Layout"], "10")),
            Initialize<CheckLayoutViewModel>("CheckLayoutDesigner_Edit", model => Populate(model, layout))
                .Location(l => l.Zone("Content", "2").Card(S["Check Designer"], "20")),
            Initialize<CheckLayoutViewModel>("CheckLayoutPayer_Edit", async model =>
            {
                Populate(model, layout);

                model.PayerAddress = postedPayerAddress ?? await _addressEditor.BuildAsync(layout.PayerAddress, AddressOptions(layout.PayerAddress?.Country));
                model.PayerAddressEditor = await _shapeFactory.CreateAddressEditorAsync(model.PayerAddress, $"{Prefix}.{nameof(CheckLayoutViewModel.PayerAddress)}");
            }).Location(l => l.Zone("Content", "3").Card(S["Payer (Blank Paper)"], "30")),
            Initialize<CheckLayoutViewModel>("CheckLayoutSignatures_Edit", model => Populate(model, layout))
                .Location(l => l.Zone("Content", "4").Card(S["Signatures"], "40")),
            Initialize<CheckLayoutViewModel>("CheckLayoutMicr_Edit", model => Populate(model, layout))
                .Location(l => l.Zone("Content", "5").Card(S["Check Line (MICR)"], "50")),
            Initialize<CheckLayoutViewModel>("CheckLayoutCalibration_Edit", model => Populate(model, layout))
                .Location(l => l.Zone("Content", "6").Card(S["Calibration"], "60")));

    private void Populate(CheckLayoutViewModel model, CheckLayout layout)
    {
        model.Name = layout.Name;
        model.Description = layout.Description;
        model.Stock = layout.Stock;
        model.Form = layout.Form;
        model.OffsetX = Text(layout.OffsetX);
        model.OffsetY = Text(layout.OffsetY);
        model.PrintMicr = layout.PrintMicr;
        model.UseAuxiliaryOnUs = layout.UseAuxiliaryOnUs;
        model.MicrFontSize = Text(layout.MicrFontSize);
        model.MicrSymbolCharacters = layout.MicrSymbolCharacters;
        model.PayerName = layout.PayerName;
        model.SecondSignatureAbove = layout.SecondSignatureAbove?.ToString(CultureInfo.InvariantCulture);
        model.HasSignature = layout.HasSignature;
        model.HasSecondSignature = layout.HasSecondSignature;
        model.HasMicrFont = layout.HasMicrFont;
        model.Fields = CheckFieldNames.All
            .Select(name =>
            {
                var position = CheckLayoutDefaults.Get(layout, name);

                return new CheckFieldPositionViewModel
                {
                    Name = name,
                    Label = Label(name),
                    X = Text(position.X),
                    Y = Text(position.Y),
                    Width = Text(position.Width),
                    FontSize = Text(position.FontSize),
                    Hidden = position.Hidden,
                };
            })
            .ToList();
        model.StockOptions =
        [
            new SelectListItem(S["Blank check paper"], nameof(CheckStock.Blank), layout.Stock == CheckStock.Blank),
            new SelectListItem(S["Pre-printed checks"], nameof(CheckStock.PrePrinted), layout.Stock == CheckStock.PrePrinted),
        ];
        model.FormOptions =
        [
            new SelectListItem(S["Check on top, two vouchers below"], nameof(CheckForm.CheckOnTop), layout.Form == CheckForm.CheckOnTop),
            new SelectListItem(S["Check in the middle, a voucher above and below"], nameof(CheckForm.CheckInMiddle), layout.Form == CheckForm.CheckInMiddle),
            new SelectListItem(S["Check on the bottom, two vouchers above"], nameof(CheckForm.CheckOnBottom), layout.Form == CheckForm.CheckOnBottom),
            new SelectListItem(S["Three checks a sheet, no voucher"], nameof(CheckForm.ThreePerPage), layout.Form == CheckForm.ThreePerPage),
        ];
    }

    public override async Task<IDisplayResult> UpdateAsync(CheckLayout layout, UpdateEditorContext context)
    {
        var model = new CheckLayoutViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        layout.Name = Clean(model.Name);
        layout.Description = Clean(model.Description);
        layout.Stock = model.Stock;
        layout.Form = model.Form;
        layout.PrintMicr = model.PrintMicr;
        layout.UseAuxiliaryOnUs = model.UseAuxiliaryOnUs;
        layout.MicrSymbolCharacters = Clean(model.MicrSymbolCharacters);
        layout.PayerName = Clean(model.PayerName);
        // The payer's address is optional; one that is entered is checked by its country's format.
        model.PayerAddress ??= new AddressEditorViewModel();

        if (!model.PayerAddress.HasAnyValue())
        {
            layout.PayerAddress = null;
        }
        else
        {
            var result = await _addressEditor.ValidateAsync(model.PayerAddress, context.Updater.ModelState, $"{Prefix}.{nameof(model.PayerAddress)}", AddressOptions(model.PayerAddress.Country));

            if (result.IsValid)
            {
                layout.PayerAddress = result.Address;
            }
        }

        await _addressEditor.PopulateAsync(model.PayerAddress, AddressOptions(model.PayerAddress.Country));

        layout.OffsetX = Number(model.OffsetX, nameof(model.OffsetX), context) ?? 0;
        layout.OffsetY = Number(model.OffsetY, nameof(model.OffsetY), context) ?? 0;
        layout.MicrFontSize = Number(model.MicrFontSize, nameof(model.MicrFontSize), context) ?? 12;

        if (string.IsNullOrWhiteSpace(model.SecondSignatureAbove))
        {
            layout.SecondSignatureAbove = null;
        }
        else if (decimal.TryParse(model.SecondSignatureAbove.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var threshold))
        {
            layout.SecondSignatureAbove = threshold;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.SecondSignatureAbove), S["Enter the amount with digits and a period, such as 5000.00."]);
        }

        foreach (var field in model.Fields ?? [])
        {
            if (string.IsNullOrEmpty(field.Name) || !CheckFieldNames.All.Contains(field.Name))
            {
                continue;
            }

            layout.Fields[field.Name] = new CheckFieldPosition
            {
                X = Number(field.X, nameof(model.Fields), context) ?? 0,
                Y = Number(field.Y, nameof(model.Fields), context) ?? 0,
                Width = Number(field.Width, nameof(model.Fields), context) ?? 0,
                FontSize = Number(field.FontSize, nameof(model.Fields), context) ?? 10,
                Hidden = field.Hidden,
            };
        }

        layout.HasSignature = await StoreFileAsync(layout, ChecksConstants.LayoutFiles.Signature, model.SignatureFile, model.RemoveSignature, layout.HasSignature, isFont: false, nameof(model.SignatureFile), context);
        layout.HasSecondSignature = await StoreFileAsync(layout, ChecksConstants.LayoutFiles.SecondSignature, model.SecondSignatureFile, model.RemoveSecondSignature, layout.HasSecondSignature, isFont: false, nameof(model.SecondSignatureFile), context);
        layout.HasMicrFont = await StoreFileAsync(layout, ChecksConstants.LayoutFiles.MicrFont, model.MicrFontFile, model.RemoveMicrFont, layout.HasMicrFont, isFont: true, nameof(model.MicrFontFile), context);

        return Editor(layout, model.PayerAddress);
    }

    private static AddressEditorOptions AddressOptions(string country)
        => new()
        {
            ShowName = false,
            ShowCompany = false,
            DefaultCountry = string.IsNullOrWhiteSpace(country) ? null : country,
        };

    private async Task<bool> StoreFileAsync(CheckLayout layout, string kind, IFormFile file, bool remove, bool stored, bool isFont, string field, UpdateEditorContext context)
    {
        if (file is { Length: > 0 })
        {
            if (file.Length > (isFont ? MaxFontBytes : MaxImageBytes))
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + field, isFont ? S["The font file is larger than 2 MB."] : S["The image is larger than 1 MB."]);

                return stored;
            }

            using var buffer = new MemoryStream();

            await file.CopyToAsync(buffer);

            var content = buffer.ToArray();

            if (isFont ? !IsTrueType(content) : !IsPicture(content))
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + field, isFont ? S["Upload a TrueType (.ttf) font file."] : S["Upload a PNG or JPEG picture."]);

                return stored;
            }

            await _files.WriteAsync(layout.ItemId, kind, content);

            return true;
        }

        if (remove && stored)
        {
            await _files.DeleteAsync(layout.ItemId, kind);

            return false;
        }

        return stored;
    }

    private double? Number(string value, string field, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        context.Updater.ModelState.AddModelError(Prefix + "." + field, S["Enter the measure in points with digits and a period, such as 36 or 12.5."]);

        return null;
    }

    private string Label(string field)
        => field switch
        {
            CheckFieldNames.Date => S["Date"],
            CheckFieldNames.Payee => S["Pay to the order of"],
            CheckFieldNames.PayeeAddress => S["Payee's address"],
            CheckFieldNames.Amount => S["Amount in figures"],
            CheckFieldNames.AmountInWords => S["Amount in words"],
            CheckFieldNames.Memo => S["Memo"],
            CheckFieldNames.Number => S["Check number"],
            CheckFieldNames.Payer => S["Payer (blank stock)"],
            CheckFieldNames.Bank => S["Bank (blank stock)"],
            CheckFieldNames.Signature => S["Signature"],
            CheckFieldNames.SecondSignature => S["Second signature"],
            _ => field,
        };

    private static bool IsPicture(byte[] content)
        => content.Length > 4
            && ((content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
                || (content[0] == 0xFF && content[1] == 0xD8));

    private static bool IsTrueType(byte[] content)
        => content.Length > 12
            && ((content[0] == 0x00 && content[1] == 0x01 && content[2] == 0x00 && content[3] == 0x00)
                || (content[0] == (byte)'t' && content[1] == (byte)'r' && content[2] == (byte)'u' && content[3] == (byte)'e'));

    private static string Text(double value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
