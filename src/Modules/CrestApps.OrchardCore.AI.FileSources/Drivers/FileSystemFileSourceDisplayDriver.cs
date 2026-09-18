using CrestApps.Core;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.FileSources.Services;
using CrestApps.OrchardCore.AI.FileSources.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.FileSources.Drivers;

/// <summary>
/// Display driver for the file-system connector settings.
/// </summary>
/// <remarks>
/// The folder is written and stored relative to this tenant's own file-source folder, and refused when it
/// resolves anywhere else. The connector repeats that check before it reads anything, because a stored
/// record may predate the rule or have been written straight into the database.
/// </remarks>
internal sealed class FileSystemFileSourceDisplayDriver : DisplayDriver<WebCrawler>
{
    private readonly ITenantFileSourceRoot _tenantRoot;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemFileSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="tenantRoot">This tenant's file-source folder.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FileSystemFileSourceDisplayDriver(
        ITenantFileSourceRoot tenantRoot,
        IStringLocalizer<FileSystemFileSourceDisplayDriver> stringLocalizer)
    {
        _tenantRoot = tenantRoot;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(WebCrawler fileSource, BuildEditorContext context)
    {
        if (!IsFileSystem(fileSource))
        {
            return null;
        }

        return Initialize<FileSystemFileSourceViewModel>("FileSystemFileSource_Edit", model =>
        {
            // Created on demand, so the path shown to a reader is a folder they can actually drop files in.
            model.TenantRootPath = _tenantRoot.EnsureRoot();

            if (fileSource.TryGet<LocalFolderIndexerMetadata>(out var metadata))
            {
                model.RootPath = metadata.RootPath;
                model.SearchPattern = metadata.SearchPattern;
                model.Recursive = metadata.Recursive;
                model.MaxItems = metadata.MaxItems;
            }

            model.IsEmpty = IsEmpty(model.RootPath);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(WebCrawler fileSource, UpdateEditorContext context)
    {
        if (!IsFileSystem(fileSource))
        {
            return null;
        }

        var model = new FileSystemFileSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // An empty value is the tenant folder itself, which is a legitimate choice and the obvious default.
        var requested = string.IsNullOrWhiteSpace(model.RootPath) ? string.Empty : model.RootPath.Trim();
        string stored = null;

        if (!_tenantRoot.TryResolve(requested, out var resolved, out var reason))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RootPath),
            S["{0} Choose this tenant's file-source folder or a folder inside it.", reason]);
        }
        else
        {
            // Stored relative to the tenant folder, so it does not go stale if the tenant is renamed or the
            // application is moved, and so nothing persists a host-absolute path a reader could edit.
            stored = _tenantRoot.ToStoredValue(resolved);
        }

        if (model.MaxItems is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxItems), S["The number of files must be a positive number."]);
        }

        fileSource.Put(new LocalFolderIndexerMetadata
        {
            RootPath = stored,
            SearchPattern = string.IsNullOrWhiteSpace(model.SearchPattern) ? "*.*" : model.SearchPattern.Trim(),
            Recursive = model.Recursive,
            MaxItems = model.MaxItems,
        });

        return Edit(fileSource, context);
    }

    /// <summary>
    /// Determines whether the resolved folder currently holds nothing, so the editor can say so rather than
    /// leave a reader waiting for a run that has nothing to do.
    /// </summary>
    /// <param name="rootPath">The configured folder.</param>
    /// <returns><see langword="true"/> when the folder resolves and holds no files.</returns>
    private bool IsEmpty(string rootPath)
    {
        if (!_tenantRoot.TryResolve(rootPath ?? string.Empty, out var resolved, out _))
        {
            return false;
        }

        try
        {
            return Directory.Exists(resolved) && !Directory.EnumerateFileSystemEntries(resolved).Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsFileSystem(WebCrawler fileSource)
        => string.Equals(fileSource.Source, FileSystemIngestionConnector.ConnectorName, StringComparison.OrdinalIgnoreCase);
}
