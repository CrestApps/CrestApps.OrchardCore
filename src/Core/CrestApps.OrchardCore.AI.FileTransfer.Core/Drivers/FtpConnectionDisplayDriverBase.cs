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
/// a port, when given, is a valid port. The password is protected, never shown, and kept when left blank while the
/// connection signs in to the same account on the same server: a change of host, port or user name drops it.
/// </summary>
/// <typeparam name="TModel">The record type the driver edits.</typeparam>
public abstract class FtpConnectionDisplayDriverBase<TModel> : DisplayDriver<TModel>
    where TModel : class
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly FileTransferHostOptions _hostOptions;
    private readonly FileTransferEgressPolicy _egressPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="FtpConnectionDisplayDriverBase{TModel}"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The provider the password is protected with.</param>
    /// <param name="stringLocalizer">The localizer of the derived driver, which owns the validation messages.</param>
    /// <param name="hostOptions">What the host's operator allows, such as accepting any FTP server certificate.</param>
    /// <param name="egressPolicy">Keeps the server off the site's own network; <see langword="null"/> checks only
    /// addresses typed as such.</param>
    protected FtpConnectionDisplayDriverBase(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer stringLocalizer,
        IOptions<FileTransferHostOptions> hostOptions,
        FileTransferEgressPolicy egressPolicy = null)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _hostOptions = hostOptions?.Value ?? new FileTransferHostOptions();
        _egressPolicy = egressPolicy ?? FileTransferEgressPolicy.AddressesOnly(hostOptions);
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
    /// <param name="existing">The stored connection, whose password is kept when none is posted and the host, port and
    /// user name are unchanged.</param>
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

        // A server on the site's own network would let the connection probe or read the host's internal services; the
        // host's operator allows it with CrestApps_AI:FileTransfer:AllowPrivateNetwork (review 11).
        if (!string.IsNullOrWhiteSpace(model.Host) && !_egressPolicy.IsAllowed(model.Host))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Host), S["This server is on the site's own network, or its name could not be found. Use a public server, or ask the host to allow a private network for file transfers."]);
        }

        // FluentFTP's PassiveAllowUnroutable (PASVUSE) connects the data channel to whatever address the server's PASV
        // answer names, even one on the site's own network, so a server could point it inside.
        if (IsUnroutablePassive(model.DataConnectionType) && !_egressPolicy.AllowPrivateNetwork)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DataConnectionType), S["This data connection type lets the server send the connection to the site's own network. Choose another one."]);
        }

        // Accepting any certificate lets anyone on the network path read the credentials and the files, so only the
        // host's operator can allow it (CrestApps_AI:FileTransfer:AllowAnyFtpCertificate).
        if (model.ValidateAnyCertificate && !_hostOptions.AllowAnyFtpCertificate)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ValidateAnyCertificate), S["Accepting any server certificate is turned off on this site. Use a server with a trusted certificate."]);
        }

        // A user name and password sent without TLS can be read on the network path, and the FTP client refuses such a
        // connection unless the host's operator allows it (CrestApps_AI:FileTransfer:AllowUnencryptedFtp).
        if (!string.IsNullOrWhiteSpace(model.Username) && !_hostOptions.AllowUnencryptedFtp && !IsEncrypted(model.EncryptionMode))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.EncryptionMode), S["Sending a user name without TLS is turned off on this site. Choose Explicit or Implicit encryption."]);
        }

        var host = model.Host?.Trim();
        var username = FileTransferFields.Trimmed(model.Username);

        // The stored password was given for one account on one server. A user who may edit the connection, but never saw
        // the password, must not be able to send it to a server of their own, so another host, port or user name drops it.
        var storedPassword = SecretDestination.Keep(existing?.Password, FileTransferFields.SameAccount(existing?.Host, existing?.Port, existing?.Username, host, model.Port, username));

        if (username is not null &&
            storedPassword is null &&
            ProtectedSettingExtensions.HasSecret(existing?.Password) &&
            string.IsNullOrWhiteSpace(model.Password))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Password), S["The server or the user name changed, so enter the password again."]);
        }

        return new FtpConnectionMetadata
        {
            Host = host,
            Port = model.Port,
            Username = username,
            Password = _dataProtectionProvider.ApplySecret(FtpResourceConstants.DataProtectionPurpose, storedPassword, model.Password),
            EncryptionMode = FileTransferFields.Trimmed(model.EncryptionMode),
            DataConnectionType = FileTransferFields.Trimmed(model.DataConnectionType),
            ValidateAnyCertificate = model.ValidateAnyCertificate,
            ConnectTimeout = model.ConnectTimeout,
            ReadTimeout = model.ReadTimeout,
            RetryAttempts = model.RetryAttempts,
        };
    }

    private static bool IsUnroutablePassive(string dataConnectionType)
        => string.Equals(dataConnectionType?.Trim(), "PassiveAllowUnroutable", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dataConnectionType?.Trim(), "PASVUSE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dataConnectionType?.Trim(), "7", StringComparison.Ordinal);

    // The FTP client's own rule: only Explicit and Implicit send the credentials over TLS.
    private static bool IsEncrypted(string encryptionMode)
        => string.Equals(encryptionMode?.Trim(), "Explicit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(encryptionMode?.Trim(), "Implicit", StringComparison.OrdinalIgnoreCase);
}
