using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Ftp.Models;
using CrestApps.OrchardCore.AI.FileTransfer.Core.ViewModels;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core.Drivers;

/// <summary>
/// The base of a driver that edits an FTP or FTPS connection stored as <see cref="FtpConnectionMetadata"/>, such as an
/// FTP file source or an FTP MCP resource. Both editors validate the connection the same way: a host is required and
/// a port, when given, is a valid port. The password is protected, never shown, and kept when left blank.
/// </summary>
/// <typeparam name="TModel">The record type the driver edits.</typeparam>
public abstract class FtpConnectionDisplayDriverBase<TModel> : DisplayDriver<TModel>
    where TModel : class
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly FileTransferHostOptions _hostOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FtpConnectionDisplayDriverBase{TModel}"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The provider the password is protected with.</param>
    /// <param name="stringLocalizer">The localizer of the derived driver, which owns the validation messages.</param>
    /// <param name="hostOptions">What the host's operator allows, such as accepting any FTP server certificate.</param>
    protected FtpConnectionDisplayDriverBase(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer stringLocalizer,
        IOptions<FileTransferHostOptions> hostOptions)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _hostOptions = hostOptions?.Value ?? new FileTransferHostOptions();
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the localizer of the derived driver.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <summary>
    /// Fills the connection fields of the editor from the stored connection.
    /// </summary>
    /// <param name="connection">The stored connection.</param>
    /// <param name="model">The editor model to fill.</param>
    protected static void PopulateConnectionFields(FtpConnectionMetadata connection, FtpConnectionFieldsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Host = connection?.Host;
        model.Port = connection?.Port;
        model.Username = connection?.Username;
        model.HasPassword = ProtectedSettingExtensions.HasSecret(connection?.Password);
        model.EncryptionMode = connection?.EncryptionMode;
        model.DataConnectionType = connection?.DataConnectionType;
        model.ValidateAnyCertificate = connection?.ValidateAnyCertificate ?? false;
        model.ConnectTimeout = connection?.ConnectTimeout;
        model.ReadTimeout = connection?.ReadTimeout;
        model.RetryAttempts = connection?.RetryAttempts;
    }

    /// <summary>
    /// Validates the posted connection fields and builds the connection to store.
    /// </summary>
    /// <param name="context">The editor update context the validation errors are added to.</param>
    /// <param name="model">The posted editor model.</param>
    /// <param name="existing">The stored connection, whose password is kept when none is posted.</param>
    /// <returns>The connection to store.</returns>
    protected FtpConnectionMetadata ApplyConnectionFields(UpdateEditorContext context, FtpConnectionFieldsViewModel model, FtpConnectionMetadata existing)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.Host))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Host), S["A host is required."]);
        }

        if (model.Port is < 1 or > 65535)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Port), S["The port must be between 1 and 65535."]);
        }

        // Accepting any certificate lets anyone on the network path read the credentials and the files, so only the
        // host's operator can allow it (CrestApps_AI:FileTransfer:AllowAnyFtpCertificate).
        if (model.ValidateAnyCertificate && !_hostOptions.AllowAnyFtpCertificate)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ValidateAnyCertificate), S["Accepting any server certificate is turned off on this site. Use a server with a trusted certificate."]);
        }

        return new FtpConnectionMetadata
        {
            Host = model.Host?.Trim(),
            Port = model.Port,
            Username = FileTransferFields.Trimmed(model.Username),
            Password = _dataProtectionProvider.ApplySecret(FtpResourceConstants.DataProtectionPurpose, existing?.Password, model.Password),
            EncryptionMode = FileTransferFields.Trimmed(model.EncryptionMode),
            DataConnectionType = FileTransferFields.Trimmed(model.DataConnectionType),
            ValidateAnyCertificate = model.ValidateAnyCertificate,
            ConnectTimeout = model.ConnectTimeout,
            ReadTimeout = model.ReadTimeout,
            RetryAttempts = model.RetryAttempts,
        };
    }
}
