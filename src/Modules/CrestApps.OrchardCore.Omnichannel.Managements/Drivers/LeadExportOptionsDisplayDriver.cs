using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Adds <c>Leave out converted leads</c> to the export form of a lead type. It is on by default, because a converted
/// lead lives on as its contact and exporting both counts the same person twice.
/// </summary>
internal sealed class LeadExportOptionsDisplayDriver : DisplayDriver<ExportRequest>
{
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadExportOptionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentTypeProvider">The CRM content type provider.</param>
    public LeadExportOptionsDisplayDriver(OmnichannelContentTypeProvider contentTypeProvider)
    {
        _contentTypeProvider = contentTypeProvider;
    }

    /// <summary>
    /// Keeps the lead options apart from the export request's own fields.
    /// </summary>
    protected override void BuildPrefix(ExportRequest model, string htmlFieldPrefix)
    {
        Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? "LeadExportOptions" : $"{htmlFieldPrefix}.LeadExportOptions";
    }

    public override IDisplayResult Edit(ExportRequest model, BuildEditorContext context)
    {
        return Initialize<LeadExportOptionsViewModel>("LeadExportOptions_Edit", async viewModel =>
        {
            viewModel.ExcludeConvertedLeads = model.Entry?.TryGet<LeadExportOptionsPart>(out var part) != true || part.ExcludeConvertedLeads;
            viewModel.LeadContentTypes = (await _contentTypeProvider.GetLeadContentTypesAsync()).ToArray();
        }).Location("Content:21");
    }

    public override async Task<IDisplayResult> UpdateAsync(ExportRequest model, UpdateEditorContext context)
    {
        var viewModel = new LeadExportOptionsViewModel();

        if (await context.Updater.TryUpdateModelAsync(viewModel, Prefix) &&
            viewModel.Rendered &&
            model.Entry is not null &&
            !string.IsNullOrEmpty(model.ContentType) &&
            (await _contentTypeProvider.GetLeadContentTypesAsync()).Contains(model.ContentType))
        {
            model.Entry.Put(new LeadExportOptionsPart { ExcludeConvertedLeads = viewModel.ExcludeConvertedLeads });
        }

        return Edit(model, context);
    }
}
