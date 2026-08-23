using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Contributes the "include last completed activity" option to the content transfer bulk export form. The
/// option is applied only when the exported content type is an omnichannel contact type, letting the export
/// append each contact's most recent completed activity of a chosen subject.
/// </summary>
public sealed class OmnichannelActivityExportDisplayDriver : DisplayDriver<ExportRequest>
{
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;
    private readonly IStringLocalizer S;

    public OmnichannelActivityExportDisplayDriver(
        ISubjectFlowSettingsService subjectFlowSettingsService,
        OmnichannelContentTypeProvider contentTypeProvider,
        IStringLocalizer<OmnichannelActivityExportDisplayDriver> stringLocalizer)
    {
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _contentTypeProvider = contentTypeProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ExportRequest model, BuildEditorContext context)
    {
        return BuildEditor(source: null);
    }

    private ShapeResult BuildEditor(OmnichannelActivityExportViewModel source)
    {
        return Initialize<OmnichannelActivityExportViewModel>("OmnichannelActivityExport_Edit", async viewModel =>
        {
            // Preserve the submitted values so a re-render (for example, after a validation error) keeps the
            // section open and the user's selections, and shows the inline validation message.
            viewModel.IncludeLastActivity = source?.IncludeLastActivity ?? false;
            viewModel.SubjectContentType = source?.SubjectContentType;
            viewModel.OnlyContactsWithLastActivity = source?.OnlyContactsWithLastActivity ?? false;

            var subjectTypes = await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync();

            viewModel.SubjectContentTypes = subjectTypes
                .Select(type => new SelectListItem(type.DisplayName, type.Name))
                .OrderBy(item => item.Text)
                .ToArray();
            viewModel.ContactContentTypes = _contentTypeProvider.GetContactContentTypes();
        }).Location("Content:20");
    }

    public override async Task<IDisplayResult> UpdateAsync(ExportRequest model, UpdateEditorContext context)
    {
        var viewModel = new OmnichannelActivityExportViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        // The option only applies to contact content types; ignore it for anything else.
        var contactContentTypes = _contentTypeProvider.GetContactContentTypes();

        if (!viewModel.IncludeLastActivity ||
            string.IsNullOrEmpty(model.ContentType) ||
            !contactContentTypes.Contains(model.ContentType))
        {
            return BuildEditor(viewModel);
        }

        if (string.IsNullOrWhiteSpace(viewModel.SubjectContentType))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.SubjectContentType), S["Select a subject to export the last activity information."]);

            return BuildEditor(viewModel);
        }

        if (await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(viewModel.SubjectContentType) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.SubjectContentType), S["The selected subject is invalid."]);

            return BuildEditor(viewModel);
        }

        model.Entry.Put(new OmnichannelActivityExportPart
        {
            IncludeLastActivity = true,
            SubjectContentType = viewModel.SubjectContentType,
            OnlyContactsWithLastActivity = viewModel.OnlyContactsWithLastActivity,
        });

        // The export handler reads the option back from the persisted entry, so force the queued path.
        model.RequiresQueue = true;

        return BuildEditor(viewModel);
    }
}
