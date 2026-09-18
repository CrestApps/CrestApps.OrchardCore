using CrestApps.Core;
using CrestApps.Core.AI.FileSources.FileTransfer;
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Ftp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.FileSources.Ftp.ViewModels;
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
internal sealed class FtpFileSourceDisplayDriver : DisplayDriver<WebCrawler>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FtpFileSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider the password is encrypted with.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FtpFileSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<FtpFileSourceDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(WebCrawler fileSource, BuildEditorContext context)
    {
        if (!IsFtp(fileSource))
        {
            return null;
        }

        return Initialize<FtpFileSourceViewModel>("FtpFileSource_Edit", model =>
        {
            var folder = fileSource.GetOrCreate<RemoteFolderIndexerMetadata>();
            model.RemoteRootPath = folder.RootPath;
            model.RemoteRecursive = folder.Recursive;
            model.RemoteMaxItems = folder.MaxItems;

            var connection = fileSource.GetOrCreate<FtpConnectionMetadata>();
            model.Host = connection.Host;
            model.Port = connection.Port;
            model.Username = connection.Username;
            model.EncryptionMode = connection.EncryptionMode;
            model.DataConnectionType = connection.DataConnectionType;
            model.ValidateAnyCertificate = connection.ValidateAnyCertificate;
            model.ConnectTimeout = connection.ConnectTimeout;
            model.ReadTimeout = connection.ReadTimeout;
            model.RetryAttempts = connection.RetryAttempts;
            model.HasPassword = !string.IsNullOrEmpty(connection.Password);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(WebCrawler fileSource, UpdateEditorContext context)
    {
        if (!IsFtp(fileSource))
        {
            return null;
        }

        var model = new FtpFileSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Host))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Host), S["A host is required."]);
        }

        if (model.Port is < 1 or > 65535)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Port), S["The port must be between 1 and 65535."]);
        }

        if (model.RemoteMaxItems is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RemoteMaxItems), S["The number of files must be a positive number."]);
        }

        var existing = fileSource.GetOrCreate<FtpConnectionMetadata>();

        fileSource.Put(new RemoteFolderIndexerMetadata
        {
            RootPath = string.IsNullOrWhiteSpace(model.RemoteRootPath) ? "/" : model.RemoteRootPath.Trim(),
            Recursive = model.RemoteRecursive,
            MaxItems = model.RemoteMaxItems,
        });

        fileSource.Put(new FtpConnectionMetadata
        {
            Host = model.Host?.Trim(),
            Port = model.Port,
            Username = Trimmed(model.Username),
            Password = string.IsNullOrWhiteSpace(model.Password)
                ? existing.Password
                : _dataProtectionProvider.CreateProtector(FtpResourceConstants.DataProtectionPurpose).Protect(model.Password),
            EncryptionMode = Trimmed(model.EncryptionMode),
            DataConnectionType = Trimmed(model.DataConnectionType),
            ValidateAnyCertificate = model.ValidateAnyCertificate,
            ConnectTimeout = model.ConnectTimeout,
            ReadTimeout = model.ReadTimeout,
            RetryAttempts = model.RetryAttempts,
        });

        return Edit(fileSource, context);
    }

    private static string Trimmed(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsFtp(WebCrawler fileSource)
        => string.Equals(fileSource.Source, FtpIngestionConnector.ConnectorName, StringComparison.OrdinalIgnoreCase);
}
