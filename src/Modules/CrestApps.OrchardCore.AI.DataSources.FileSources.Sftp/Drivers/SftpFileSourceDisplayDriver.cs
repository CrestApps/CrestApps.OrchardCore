using CrestApps.Core;
using CrestApps.Core.AI.FileSources.FileTransfer;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;
using CrestApps.OrchardCore.AI.DataSources.FileSources.Sftp.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Sftp.Drivers;

/// <summary>
/// Display driver for the SFTP connector settings.
/// </summary>
/// <remarks>
/// No secret travels back to the browser -- not the password, not the private key, not either passphrase.
/// The editor is told only that one is stored, and a blank submission keeps whatever is there; otherwise
/// opening the form and pressing Save would erase the key.
/// </remarks>
internal sealed class SftpFileSourceDisplayDriver : DisplayDriver<WebCrawler>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SftpFileSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider the secrets are encrypted with.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SftpFileSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<SftpFileSourceDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(WebCrawler fileSource, BuildEditorContext context)
    {
        if (!IsSftp(fileSource))
        {
            return null;
        }

        return Initialize<SftpFileSourceViewModel>("SftpFileSource_Edit", model =>
        {
            var folder = fileSource.GetOrCreate<RemoteFolderIndexerMetadata>();
            model.RemoteRootPath = folder.RootPath;
            model.RemoteRecursive = folder.Recursive;
            model.RemoteMaxItems = folder.MaxItems;

            var connection = fileSource.GetOrCreate<SftpConnectionMetadata>();
            model.Host = connection.Host;
            model.Port = connection.Port;
            model.Username = connection.Username;
            model.ProxyType = connection.ProxyType;
            model.ProxyHost = connection.ProxyHost;
            model.ProxyPort = connection.ProxyPort;
            model.ProxyUsername = connection.ProxyUsername;
            model.ConnectionTimeout = connection.ConnectionTimeout;
            model.KeepAliveInterval = connection.KeepAliveInterval;

            model.HasPassword = !string.IsNullOrEmpty(connection.Password);
            model.HasPrivateKey = !string.IsNullOrEmpty(connection.PrivateKey);
            model.HasPassphrase = !string.IsNullOrEmpty(connection.Passphrase);
            model.HasProxyPassword = !string.IsNullOrEmpty(connection.ProxyPassword);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(WebCrawler fileSource, UpdateEditorContext context)
    {
        if (!IsSftp(fileSource))
        {
            return null;
        }

        var model = new SftpFileSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Host))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Host), S["A host is required."]);
        }

        if (model.Port is < 1 or > 65535)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Port), S["The port must be between 1 and 65535."]);
        }

        if (model.ProxyPort is < 1 or > 65535)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProxyPort), S["The proxy port must be between 1 and 65535."]);
        }

        if (model.RemoteMaxItems is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RemoteMaxItems), S["The number of files must be a positive number."]);
        }

        var existing = fileSource.GetOrCreate<SftpConnectionMetadata>();
        var protector = _dataProtectionProvider.CreateProtector(SftpResourceConstants.DataProtectionPurpose);

        fileSource.Put(new RemoteFolderIndexerMetadata
        {
            RootPath = string.IsNullOrWhiteSpace(model.RemoteRootPath) ? "/" : model.RemoteRootPath.Trim(),
            Recursive = model.RemoteRecursive,
            MaxItems = model.RemoteMaxItems,
        });

        fileSource.Put(new SftpConnectionMetadata
        {
            Host = model.Host?.Trim(),
            Port = model.Port,
            Username = Trimmed(model.Username),
            Password = Keep(protector, model.Password, existing.Password),
            PrivateKey = Keep(protector, model.PrivateKey, existing.PrivateKey),
            Passphrase = Keep(protector, model.Passphrase, existing.Passphrase),
            ProxyType = Trimmed(model.ProxyType),
            ProxyHost = Trimmed(model.ProxyHost),
            ProxyPort = model.ProxyPort,
            ProxyUsername = Trimmed(model.ProxyUsername),
            ProxyPassword = Keep(protector, model.ProxyPassword, existing.ProxyPassword),
            ConnectionTimeout = model.ConnectionTimeout,
            KeepAliveInterval = model.KeepAliveInterval,
        });

        return Edit(fileSource, context);
    }

    /// <summary>
    /// Encrypts a newly typed secret, or keeps the stored one when the field came back empty.
    /// </summary>
    /// <param name="protector">The data protector.</param>
    /// <param name="submitted">What the form submitted.</param>
    /// <param name="stored">What is already stored.</param>
    /// <returns>The value to store.</returns>
    private static string Keep(IDataProtector protector, string submitted, string stored)
        => string.IsNullOrWhiteSpace(submitted) ? stored : protector.Protect(submitted);

    private static string Trimmed(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsSftp(WebCrawler fileSource)
        => string.Equals(fileSource.Source, SftpIngestionConnector.ConnectorName, StringComparison.OrdinalIgnoreCase);
}
