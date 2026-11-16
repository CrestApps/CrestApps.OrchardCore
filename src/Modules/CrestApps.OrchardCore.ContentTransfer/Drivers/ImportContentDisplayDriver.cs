using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContentTransfer.Drivers;

public sealed class ImportContentDisplayDriver : DisplayDriver<ImportContent>
{
    private readonly IReadOnlyList<IContentTransferFileFormatProvider> _formatProviders;

    internal readonly IStringLocalizer S;

    public ImportContentDisplayDriver(
        IEnumerable<IContentTransferFileFormatProvider> formatProviders,
        IStringLocalizer<ImportContentDisplayDriver> stringLocalizer)
    {
        _formatProviders = formatProviders
            .OrderBy(provider => provider.FileExtension, StringComparer.OrdinalIgnoreCase)
            .ToList();
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> EditAsync(ImportContent model, BuildEditorContext context)
        => Task.FromResult<IDisplayResult>(
            Initialize<ContentImportViewModel>("ImportContentFile_Edit", viewModel =>
            {
                var options = model.GetOrCreate<ImportContentOptionsPart>();
                viewModel.File = model.File;
                viewModel.PublishImportedContent = options.PublishImportedContent;
                viewModel.AcceptedFileTypes = string.Join(',', AllowedExtensions(model));
            })
            .Location("Content:1"));

    public override async Task<IDisplayResult> UpdateAsync(ImportContent model, UpdateEditorContext context)
    {
        var viewModel = new ContentImportViewModel();

        if (await context.Updater.TryUpdateModelAsync(viewModel, Prefix))
        {
            var allowedExtensions = AllowedExtensions(model);

            if (viewModel.File?.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.File), S["File is required."]);
            }
            else if (allowedExtensions.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.File), S["No import file formats are currently enabled."]);
            }
            else
            {
                var extension = Path.GetExtension(viewModel.File.FileName);

                if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    context.Updater.ModelState.AddModelError(
                        Prefix,
                        nameof(viewModel.File),
                        S["Only the enabled file formats are supported: {0}.", string.Join(", ", allowedExtensions)]);
                }

                model.File = viewModel.File;
            }

            model.Alter<ImportContentOptionsPart>(options =>
            {
                options.PublishImportedContent = viewModel.PublishImportedContent;
            });
        }

        return await EditAsync(model, context);
    }

    // A format may import some content types only, such as a package of questions.
    private List<string> AllowedExtensions(ImportContent model)
        => _formatProviders
            .Where(provider => string.IsNullOrEmpty(model.ContentTypeName) || provider.CanImport(model.ContentTypeName))
            .Select(provider => provider.FileExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
