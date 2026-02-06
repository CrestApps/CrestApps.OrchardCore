using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Models;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Drivers;

public sealed class SftpResourceDisplayDriver : DisplayDriver<McpResource>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public SftpResourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<SftpResourceDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(McpResource resource, BuildEditorContext context)
    {
        if (resource.Source != SftpResourceConstants.Type)
        {
            return null;
        }

        return Initialize<SftpConnectionViewModel>("SftpResourceConnection_Edit", model =>
        {
            var metadata = resource.As<SftpConnectionMetadata>();

            model.Host = metadata?.Host;
            model.Port = metadata?.Port;
            model.Username = metadata?.Username;
            model.HasPassword = !string.IsNullOrEmpty(metadata?.Password);
            model.HasPrivateKey = !string.IsNullOrEmpty(metadata?.PrivateKey);
            model.HasPassphrase = !string.IsNullOrEmpty(metadata?.Passphrase);
            model.ProxyType = metadata?.ProxyType;
            model.ProxyHost = metadata?.ProxyHost;
            model.ProxyPort = metadata?.ProxyPort;
            model.ProxyUsername = metadata?.ProxyUsername;
            model.HasProxyPassword = !string.IsNullOrEmpty(metadata?.ProxyPassword);
            model.ConnectionTimeout = metadata?.ConnectionTimeout;
            model.KeepAliveInterval = metadata?.KeepAliveInterval;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpResource resource, UpdateEditorContext context)
    {
        if (resource.Source != SftpResourceConstants.Type)
        {
            return null;
        }

        var model = new SftpConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Host))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Host), S["The SFTP host is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Username))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Username), S["The SFTP username is required."]);
        }

        var metadata = resource.As<SftpConnectionMetadata>();
        var protector = _dataProtectionProvider.CreateProtector(SftpResourceConstants.DataProtectionPurpose);

        var hasNewPassword = !string.IsNullOrWhiteSpace(model.Password);
        var hasNewPrivateKey = !string.IsNullOrWhiteSpace(model.PrivateKey);
        var hasNewPassphrase = !string.IsNullOrWhiteSpace(model.Passphrase);
        var hasNewProxyPassword = !string.IsNullOrWhiteSpace(model.ProxyPassword);

        // Require at least one authentication method
        var hasExistingAuth = !string.IsNullOrEmpty(metadata?.Password) || !string.IsNullOrEmpty(metadata?.PrivateKey);
        if (!hasNewPassword && !hasNewPrivateKey && !hasExistingAuth)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Password), S["Please provide a password or private key for authentication."]);
        }

        string protectedPassword = null;
        if (hasNewPassword)
        {
            protectedPassword = protector.Protect(model.Password);
        }

        string protectedPrivateKey = null;
        if (hasNewPrivateKey)
        {
            protectedPrivateKey = protector.Protect(model.PrivateKey);
        }

        string protectedPassphrase = null;
        if (hasNewPassphrase)
        {
            protectedPassphrase = protector.Protect(model.Passphrase);
        }

        string protectedProxyPassword = null;
        if (hasNewProxyPassword)
        {
            protectedProxyPassword = protector.Protect(model.ProxyPassword);
        }

        resource.Alter<SftpConnectionMetadata>(m =>
        {
            m.Host = model.Host;
            m.Port = model.Port;
            m.Username = model.Username;
            m.ProxyType = model.ProxyType;
            m.ProxyHost = model.ProxyHost;
            m.ProxyPort = model.ProxyPort;
            m.ProxyUsername = model.ProxyUsername;
            m.ConnectionTimeout = model.ConnectionTimeout;
            m.KeepAliveInterval = model.KeepAliveInterval;

            if (hasNewPassword)
            {
                m.Password = protectedPassword;
            }

            if (hasNewPrivateKey)
            {
                m.PrivateKey = protectedPrivateKey;
            }

            if (hasNewPassphrase)
            {
                m.Passphrase = protectedPassphrase;
            }

            if (hasNewProxyPassword)
            {
                m.ProxyPassword = protectedProxyPassword;
            }
        });

        return Edit(resource, context);
    }
}
