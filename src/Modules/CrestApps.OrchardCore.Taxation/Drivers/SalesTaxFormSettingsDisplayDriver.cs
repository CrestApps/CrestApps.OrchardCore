using CrestApps.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// With Tax Returns: the Sales tax return card of the tax form editor, for a form whose source is sales tax returns:
/// which of its fields is the amount the return says is due.
/// </summary>
internal sealed class SalesTaxFormSettingsDisplayDriver : DisplayDriver<TaxForm>
{
    public override IDisplayResult Edit(TaxForm form, BuildEditorContext context)
        => Initialize<SalesTaxFormSettingsViewModel>("SalesTaxFormSettings_Edit", model =>
        {
            model.AmountDueField = form.GetSalesTaxSettings().AmountDueField ?? SalesTaxFormSettings.DefaultAmountDueField;
            model.FilingFields = [.. form.Fields
                .Where(field => field.Level == TaxFormFieldLevel.Filing && !string.IsNullOrWhiteSpace(field.Number))
                .Select(field => field.Number)];
        }).Location("Content:2")
        .RenderWhen(() => Task.FromResult(IsSalesTax(form.Source)));

    public override async Task<IDisplayResult> UpdateAsync(TaxForm form, UpdateEditorContext context)
    {
        var model = new SalesTaxFormSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // The source is posted with the form's own fields, whichever driver reads it first.
        if (!IsSalesTax(model.Source ?? form.Source))
        {
            return Edit(form, context);
        }

        form.Put(new SalesTaxFormSettings
        {
            AmountDueField = string.IsNullOrWhiteSpace(model.AmountDueField) ? null : model.AmountDueField.Trim(),
        });

        return Edit(form, context);
    }

    private static bool IsSalesTax(string source)
        => string.Equals(source?.Trim(), TaxationConstants.SalesTaxFormSource, StringComparison.OrdinalIgnoreCase);
}
