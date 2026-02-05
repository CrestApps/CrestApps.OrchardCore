using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Models;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Drivers;

public sealed class FtpResourceDisplayDriver : DisplayDriver<McpResource>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public FtpResourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<FtpResourceDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(McpResource resource, BuildEditorContext context)
    {
        if (resource.Source != FtpResourceConstants.Type)
        {
            return null;
        }

        return Initialize<FtpConnectionViewModel>("FtpResourceConnection_Edit", model =>
        {
            var metadata = resource.As<FtpConnectionMetadata>();

            model.Host = metadata?.Host;
            model.Port = metadata?.Port;
            model.Username = metadata?.Username;
            model.HasPassword = !string.IsNullOrEmpty(metadata?.Password);
            model.UseSsl = metadata?.UseSsl ?? false;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpResource resource, UpdateEditorContext context)
    {
        if (resource.Source != FtpResourceConstants.Type)
        {
            return null;
        }

        var model = new FtpConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Host))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Host), S["The FTP host is required."]);
        }

        var metadata = resource.As<FtpConnectionMetadata>();

        var hasNewPassword = !string.IsNullOrWhiteSpace(model.Password);

        if (!hasNewPassword && string.IsNullOrWhiteSpace(metadata?.Password))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Password), S["The FTP password is required."]);
        }

        if (hasNewPassword)
        {
            var protector = _dataProtectionProvider.CreateProtector(FtpResourceConstants.DataProtectionPurpose);

            metadata ??= new FtpConnectionMetadata();
            metadata.Password = protector.Protect(model.Password);
        }

        resource.Alter<FtpConnectionMetadata>(m =>
        {
            m.Host = model.Host;
            m.Port = model.Port;
            m.Username = model.Username;
            m.UseSsl = model.UseSsl;

            if (hasNewPassword)
            {
                m.Password = metadata.Password;
            }
        });

        return Edit(resource, context);
    }
}
