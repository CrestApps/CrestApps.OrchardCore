using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;
using CrestApps.OrchardCore.AI.FileTransfer.Core.ViewModels;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core.Drivers;

/// <summary>
/// The base of a driver that edits an SFTP connection stored as <see cref="SftpConnectionMetadata"/>, such as an SFTP
/// file source or an SFTP MCP resource. Both editors validate the connection the same way: a host and a username are
/// required, ports are valid ports, and a password or a private key is stored. Secrets are protected, never shown, and
/// kept when left blank while they are used with the same account: the password, private key and passphrase while the
/// host, port, user name and host key fingerprint are unchanged, and the proxy password while the proxy is.
/// </summary>
/// <typeparam name="TModel">The record type the driver edits.</typeparam>
public abstract class SftpConnectionDisplayDriverBase<TModel> : DisplayDriver<TModel>
    where TModel : class
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SftpConnectionDisplayDriverBase{TModel}"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The provider the secrets are protected with.</param>
    /// <param name="stringLocalizer">The localizer of the derived driver, which owns the validation messages.</param>
    protected SftpConnectionDisplayDriverBase(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
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
    protected static void PopulateConnectionFields(SftpConnectionMetadata connection, SftpConnectionFieldsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Host = connection?.Host;
        model.Port = connection?.Port;
        model.Username = connection?.Username;
        model.HostKeyFingerprint = connection?.HostKeyFingerprint;
        model.HasPassword = ProtectedSettingExtensions.HasSecret(connection?.Password);
        model.HasPrivateKey = ProtectedSettingExtensions.HasSecret(connection?.PrivateKey);
        model.HasPassphrase = ProtectedSettingExtensions.HasSecret(connection?.Passphrase);
        model.ProxyType = connection?.ProxyType;
        model.ProxyHost = connection?.ProxyHost;
        model.ProxyPort = connection?.ProxyPort;
        model.ProxyUsername = connection?.ProxyUsername;
        model.HasProxyPassword = ProtectedSettingExtensions.HasSecret(connection?.ProxyPassword);
        model.ConnectionTimeout = connection?.ConnectionTimeout;
        model.KeepAliveInterval = connection?.KeepAliveInterval;
    }

    /// <summary>
    /// Validates the posted connection fields and builds the connection to store.
    /// </summary>
    /// <param name="context">The editor update context the validation errors are added to.</param>
    /// <param name="model">The posted editor model.</param>
    /// <param name="existing">The stored connection, whose secrets are kept when none is posted and they are used with the
    /// same account.</param>
    /// <returns>The connection to store.</returns>
    protected SftpConnectionMetadata ApplyConnectionFields(UpdateEditorContext context, SftpConnectionFieldsViewModel model, SftpConnectionMetadata existing)
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

        if (string.IsNullOrWhiteSpace(model.Username))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Username), S["A username is required."]);
        }

        if (model.ProxyPort is < 1 or > 65535)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProxyPort), S["The proxy port must be between 1 and 65535."]);
        }

        // The client trusts the server only when it shows one of these keys, and refuses a connection that names none, so
        // a connection saved without one, or with one the client never matches, could never connect.
        if (!SftpHostKeyFingerprints.IsValid(model.HostKeyFingerprint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.HostKeyFingerprint), S["Enter the server's host key fingerprint, as ssh-keygen -lf prints it (SHA256:... or MD5:...). Separate several with commas."]);
        }

        var connection = new SftpConnectionMetadata
        {
            Host = model.Host?.Trim(),
            Port = model.Port,
            Username = FileTransferFields.Trimmed(model.Username),
            HostKeyFingerprint = SftpHostKeyFingerprints.Normalize(model.HostKeyFingerprint),
            ProxyType = FileTransferFields.Trimmed(model.ProxyType),
            ProxyHost = FileTransferFields.Trimmed(model.ProxyHost),
            ProxyPort = model.ProxyPort,
            ProxyUsername = FileTransferFields.Trimmed(model.ProxyUsername),
            ConnectionTimeout = model.ConnectionTimeout,
            KeepAliveInterval = model.KeepAliveInterval,
        };

        // The stored password and key were given for one account on the server the host key identifies. A user who may
        // edit the connection, but never saw them, must not be able to send the password to a server of their own, or
        // sign in with the key to another server or account, so another host, port, user name or host key drops them.
        // The host key counts too: trusting another key lets a server that stands in for the host receive the password.
        var sameAccount = existing is not null &&
            FileTransferFields.SameAccount(existing.Host, existing.Port, existing.Username, connection.Host, connection.Port, connection.Username) &&
            SecretDestination.SameValue(existing.HostKeyFingerprint, connection.HostKeyFingerprint);

        // The proxy password goes to the proxy, so it is kept while the proxy and its account are the same.
        var sameProxy = existing is not null &&
            SecretDestination.SameValue(existing.ProxyType, connection.ProxyType) &&
            FileTransferFields.SameAccount(existing.ProxyHost, existing.ProxyPort, existing.ProxyUsername, connection.ProxyHost, connection.ProxyPort, connection.ProxyUsername);

        connection.Password = ApplySecret(SecretDestination.Keep(existing?.Password, sameAccount), model.Password);
        connection.PrivateKey = ApplySecret(SecretDestination.Keep(existing?.PrivateKey, sameAccount), model.PrivateKey);
        connection.Passphrase = ApplySecret(SecretDestination.Keep(existing?.Passphrase, sameAccount), model.Passphrase);
        connection.ProxyPassword = ApplySecret(SecretDestination.Keep(existing?.ProxyPassword, sameProxy), model.ProxyPassword);

        if (!ProtectedSettingExtensions.HasSecret(connection.Password) && !ProtectedSettingExtensions.HasSecret(connection.PrivateKey))
        {
            var dropped = !sameAccount && (ProtectedSettingExtensions.HasSecret(existing?.Password) || ProtectedSettingExtensions.HasSecret(existing?.PrivateKey));

            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Password), dropped
                ? S["The server, the user name or the host key changed, so enter the password or the private key again."]
                : S["Provide a password or a private key."]);
        }

        return connection;
    }

    private string ApplySecret(string existing, string posted)
        => _dataProtectionProvider.ApplySecret(SftpResourceConstants.DataProtectionPurpose, existing, posted);
}
