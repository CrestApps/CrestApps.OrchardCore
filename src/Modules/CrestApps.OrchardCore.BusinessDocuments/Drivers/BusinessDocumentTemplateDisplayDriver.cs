using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.BusinessDocuments.Drivers;

/// <summary>
/// The rows of the document templates list and the fields of the template editor.
/// </summary>
internal sealed class BusinessDocumentTemplateDisplayDriver : DisplayDriver<BusinessDocumentTemplate>
{
    private const string DefaultAccentColor = "#2563EB";

    private readonly BusinessDocumentOptions _options;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public BusinessDocumentTemplateDisplayDriver(
        IOptions<BusinessDocumentOptions> options,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<BusinessDocumentTemplateDisplayDriver> stringLocalizer)
    {
        _options = options.Value;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(BusinessDocumentTemplate template, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = template.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("BusinessDocumentTemplate_Fields_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("BusinessDocumentTemplate_Description_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(template.Description))),
            Initialize<BusinessDocumentTemplateTagsViewModel>("BusinessDocumentTemplate_Tags_SummaryAdmin", model =>
            {
                model.Template = template;
                model.KindTitles = (template.Kinds ?? [])
                    .Select(kind => _options.Kinds.TryGetValue(kind, out var registered) ? GetTitle(registered) : kind)
                    .ToList();
            })
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Kinds:5"),
            View("BusinessDocumentTemplate_Buttons_SummaryAdmin", template).Location("Actions:5"),
            View("BusinessDocumentTemplate_Meta_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"));
    }

    public override IDisplayResult Edit(BusinessDocumentTemplate template, BuildEditorContext context)
        => Initialize<BusinessDocumentTemplateViewModel>("BusinessDocumentTemplateFields_Edit", model =>
        {
            model.Name = template.Name;
            model.Description = template.Description;
            model.Kinds = [.. template.Kinds ?? []];
            model.IsDefault = template.IsDefault;
            model.LogoUrl = template.LogoUrl;
            model.UseAccentColor = !string.IsNullOrWhiteSpace(template.AccentColor);
            model.AccentColor = BusinessDocumentStyle.TryNormalizeAccentColor(template.AccentColor, out var color) ? color : DefaultAccentColor;
            model.PaperSize = template.PaperSize;
            model.Footer = template.Footer;
            model.KindOptions = GetKindOptions(template.Kinds);
            model.PaperSizeOptions =
            [
                new SelectListItem(S["The renderer's default (Letter)"], nameof(BusinessDocumentPaperSize.Default)),
                new SelectListItem(S["Letter"], nameof(BusinessDocumentPaperSize.Letter)),
                new SelectListItem(S["A4"], nameof(BusinessDocumentPaperSize.A4)),
            ];
        }).Location("Content:1");

    public override async Task<IDisplayResult> UpdateAsync(BusinessDocumentTemplate template, UpdateEditorContext context)
    {
        var model = new BusinessDocumentTemplateViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        template.Name = model.Name?.Trim();
        template.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        template.Kinds = (model.Kinds ?? [])
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        template.IsDefault = model.IsDefault;
        template.LogoUrl = string.IsNullOrWhiteSpace(model.LogoUrl) ? null : model.LogoUrl.Trim();
        template.AccentColor = model.UseAccentColor ? model.AccentColor?.Trim() : null;
        template.PaperSize = model.PaperSize;
        template.Footer = string.IsNullOrWhiteSpace(model.Footer) ? null : model.Footer.Trim();

        return Edit(template, context);
    }

    // The registered kinds, and the kinds the template names that no enabled feature registers, so saving the template
    // does not drop them.
    private List<SelectListItem> GetKindOptions(List<string> selected)
    {
        var chosen = new HashSet<string>(selected ?? [], StringComparer.OrdinalIgnoreCase);
        var options = _options.Kinds.Values
            .Select(kind => new SelectListItem(GetTitle(kind), kind.Name, chosen.Contains(kind.Name)))
            .OrderBy(option => option.Text, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var kind in chosen.Where(kind => !_options.Kinds.ContainsKey(kind)).Order(StringComparer.OrdinalIgnoreCase))
        {
            options.Add(new SelectListItem(kind, kind, selected: true));
        }

        return options;
    }

    private string GetTitle(BusinessDocumentKind kind)
        => kind.Title is null ? kind.Name : _localizerFactory.Localize(kind.Title).Value;
}
