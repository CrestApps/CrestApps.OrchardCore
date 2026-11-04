using CrestApps.Core;
using CrestApps.Core.AI.FileSources.FileTransfer;
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Ftp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.FileSources.Ftp.ViewModels;
using CrestApps.OrchardCore.AI.FileTransfer.Core.Drivers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.FileSources.Ftp.Drivers;

/// <summary>
/// Display driver for the FTP/FTPS connector settings.
/// </summary>
/// <remarks>
/// The password never travels back to the browser. The editor is told only that one is stored, and a blank
/// submission keeps whatever is there -- otherwise opening the form and pressing Save would erase it.
/// </remarks>
internal sealed class FtpFileSourceDisplayDriver : FtpConnectionDisplayDriverBase<FileSource>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FtpFileSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider the password is encrypted with.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FtpFileSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<FtpFileSourceDisplayDriver> stringLocalizer)
        : base(dataProtectionProvider, stringLocalizer)
    {
    }

    public override IDisplayResult Edit(FileSource fileSource, BuildEditorContext context)
    {
        if (!IsFtp(fileSource))
        {
            return null;
        }

        return Initialize<FtpFileSourceViewModel>("FtpFileSource_Edit", model =>
        {
            var folder = fileSource.GetOrCreate<RemoteFileSourceMetadata>();
            model.RemoteRootPath = folder.RootPath;
            model.RemoteRecursive = folder.Recursive;
            model.RemoteMaxItems = folder.MaxItems;

            PopulateConnectionFields(fileSource.GetOrCreate<FtpConnectionMetadata>(), model);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(FileSource fileSource, UpdateEditorContext context)
    {
        if (!IsFtp(fileSource))
        {
            return null;
        }

        var model = new FtpFileSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.RemoteMaxItems is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RemoteMaxItems), S["The number of files must be a positive number."]);
        }

        var connection = ApplyConnectionFields(context, model, fileSource.GetOrCreate<FtpConnectionMetadata>());

        fileSource.Put(new RemoteFileSourceMetadata
        {
            RootPath = string.IsNullOrWhiteSpace(model.RemoteRootPath) ? "/" : model.RemoteRootPath.Trim(),
            Recursive = model.RemoteRecursive,
            MaxItems = model.RemoteMaxItems,
        });

        fileSource.Put(connection);

        return Edit(fileSource, context);
    }

    private static bool IsFtp(FileSource fileSource)
        => string.Equals(fileSource.Source, FtpIngestionConnector.ConnectorName, StringComparison.OrdinalIgnoreCase);
}
