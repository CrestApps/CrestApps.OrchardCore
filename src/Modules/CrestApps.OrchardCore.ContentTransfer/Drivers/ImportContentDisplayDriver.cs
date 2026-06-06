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
    private readonly HashSet<string> _allowedExtensions;
    private readonly string _acceptedFileTypes;
    private readonly string _supportedExtensions;

    internal readonly IStringLocalizer S;

    public ImportContentDisplayDriver(
        IEnumerable<IContentTransferFileFormatProvider> formatProviders,
        IStringLocalizer<ImportContentDisplayDriver> stringLocalizer)
    {
        var orderedProviders = formatProviders
            .OrderBy(provider => provider.FileExtension, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _allowedExtensions = orderedProviders
            .Select(provider => provider.FileExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _acceptedFileTypes = string.Join(',', orderedProviders.Select(provider => provider.FileExtension));
        _supportedExtensions = string.Join(", ", orderedProviders.Select(provider => provider.FileExtension));
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> EditAsync(ImportContent model, BuildEditorContext context)
        => Task.FromResult<IDisplayResult>(
            Initialize<ContentImportViewModel>("ImportContentFile_Edit", viewModel =>
            {
                var options = model.GetOrCreate<ImportContentOptionsPart>();
                viewModel.File = model.File;
                viewModel.PublishImportedContent = options.PublishImportedContent;
                viewModel.AcceptedFileTypes = _acceptedFileTypes;
            })
            .Location("Content:1"));

    public override async Task<IDisplayResult> UpdateAsync(ImportContent model, UpdateEditorContext context)
    {
        var viewModel = new ContentImportViewModel();

        if (await context.Updater.TryUpdateModelAsync(viewModel, Prefix))
        {
            if (viewModel.File?.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.File), S["File is required."]);
            }
            else if (_allowedExtensions.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.File), S["No import file formats are currently enabled."]);
            }
            else
            {
                var extension = Path.GetExtension(viewModel.File.FileName);

                if (!_allowedExtensions.Contains(extension))
                {
                    context.Updater.ModelState.AddModelError(
                        Prefix,
                        nameof(viewModel.File),
                        S["Only the enabled file formats are supported: {0}.", _supportedExtensions]);
                }

                model.File = viewModel.File;
            }

            var options = model.GetOrCreate<ImportContentOptionsPart>();
            options.PublishImportedContent = viewModel.PublishImportedContent;
            model.Put(options);
        }

        return await EditAsync(model, context);
    }
}
