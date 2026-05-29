using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Display driver that adds omnichannel-specific import options to the import form
/// when the content type being imported has the <c>OmnichannelContactPart</c>.
/// </summary>
public sealed class OmnichannelContactImportOptionsDisplayDriver : DisplayDriver<ImportContent>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactImportOptionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public OmnichannelContactImportOptionsDisplayDriver(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override async Task<IDisplayResult> EditAsync(ImportContent model, BuildEditorContext context)
    {
        if (!await IsOmnichannelContactAsync(model))
        {
            return null;
        }

        var options = model.GetOrCreate<OmnichannelContactImportOptionsPart>();

        return Initialize<OmnichannelContactImportOptionsViewModel>("OmnichannelContactImportOptions_Edit", viewModel =>
        {
            viewModel.IgnoreDuplicateByPhoneNumber = options.IgnoreDuplicateByPhoneNumber;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ImportContent model, UpdateEditorContext context)
    {
        if (!await IsOmnichannelContactAsync(model))
        {
            return null;
        }

        var viewModel = new OmnichannelContactImportOptionsViewModel();

        if (await context.Updater.TryUpdateModelAsync(viewModel, Prefix))
        {
            var options = model.GetOrCreate<OmnichannelContactImportOptionsPart>();
            options.IgnoreDuplicateByPhoneNumber = viewModel.IgnoreDuplicateByPhoneNumber;
            model.Put(options);
        }

        return await EditAsync(model, context);
    }

    private async Task<bool> IsOmnichannelContactAsync(ImportContent model)
    {
        if (string.IsNullOrEmpty(model.ContentTypeId))
        {
            return false;
        }

        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(model.ContentTypeId);

        return contentTypeDefinition?.Parts?.Any(p =>
            p.PartDefinition.Name == OmnichannelConstants.ContentParts.OmnichannelContact) == true;
    }
}
