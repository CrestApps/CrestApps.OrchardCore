using CrestApps.Core;
using CrestApps.Core.AI.FileSources.FileTransfer;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;
using CrestApps.OrchardCore.AI.FileSources.Sftp.ViewModels;
using CrestApps.OrchardCore.AI.FileTransfer.Core.Drivers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.FileSources.Sftp.Drivers;

/// <summary>
/// Display driver for the SFTP connector settings.
/// </summary>
/// <remarks>
/// No secret travels back to the browser -- not the password, not the private key, not either passphrase.
/// The editor is told only that one is stored, and a blank submission keeps whatever is there; otherwise
/// opening the form and pressing Save would erase the key.
/// </remarks>
internal sealed class SftpFileSourceDisplayDriver : SftpConnectionDisplayDriverBase<FileSource>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SftpFileSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider the secrets are encrypted with.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SftpFileSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<SftpFileSourceDisplayDriver> stringLocalizer)
        : base(dataProtectionProvider, stringLocalizer)
    {
    }

    public override IDisplayResult Edit(FileSource fileSource, BuildEditorContext context)
    {
        if (!IsSftp(fileSource))
        {
            return null;
        }

        return Initialize<SftpFileSourceViewModel>("SftpFileSource_Edit", model =>
        {
            var folder = fileSource.GetOrCreate<RemoteFileSourceMetadata>();
            model.RemoteRootPath = folder.RootPath;
            model.RemoteRecursive = folder.Recursive;
            model.RemoteMaxItems = folder.MaxItems;

            PopulateConnectionFields(fileSource.GetOrCreate<SftpConnectionMetadata>(), model);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(FileSource fileSource, UpdateEditorContext context)
    {
        if (!IsSftp(fileSource))
        {
            return null;
        }

        var model = new SftpFileSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.RemoteMaxItems is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RemoteMaxItems), S["The number of files must be a positive number."]);
        }

        var connection = ApplyConnectionFields(context, model, fileSource.GetOrCreate<SftpConnectionMetadata>());

        fileSource.Put(new RemoteFileSourceMetadata
        {
            RootPath = string.IsNullOrWhiteSpace(model.RemoteRootPath) ? "/" : model.RemoteRootPath.Trim(),
            Recursive = model.RemoteRecursive,
            MaxItems = model.RemoteMaxItems,
        });

        fileSource.Put(connection);

        return Edit(fileSource, context);
    }

    private static bool IsSftp(FileSource fileSource)
        => string.Equals(fileSource.Source, SftpIngestionConnector.ConnectorName, StringComparison.OrdinalIgnoreCase);
}
