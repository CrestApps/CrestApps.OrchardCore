using CrestApps.Core;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;
using CrestApps.OrchardCore.AI.FileTransfer.Core.Drivers;
using CrestApps.OrchardCore.AI.FileTransfer.Core.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Drivers;

/// <summary>
/// Display driver for the SFTP connection of an SFTP MCP resource.
/// </summary>
public sealed class SftpResourceDisplayDriver : SftpConnectionDisplayDriverBase<McpResource>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SftpResourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SftpResourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<SftpResourceDisplayDriver> stringLocalizer)
        : base(dataProtectionProvider, stringLocalizer)
    {
    }

    public override IDisplayResult Edit(McpResource resource, BuildEditorContext context)
    {
        if (resource.Source != SftpResourceConstants.Type)
        {
            return null;
        }

        return Combine(
            Initialize<SftpConnectionFieldsViewModel>("SftpResourceServer_Edit", model => Populate(resource, model))
                .Location(l => l.Zone("Content", "5").Card(S["Server"], "40")),
            Initialize<SftpConnectionFieldsViewModel>("SftpResourceAuthentication_Edit", model => Populate(resource, model))
                .Location(l => l.Zone("Content", "5.05").Card(S["Authentication"], "45")),
            Initialize<SftpConnectionFieldsViewModel>("SftpResourceConnectionOptions_Edit", model => Populate(resource, model))
                .Location(l => l.Zone("Content", "5.2").Card(S["Connection Options"], "60")),
            Initialize<SftpConnectionFieldsViewModel>("SftpResourceProxy_Edit", model => Populate(resource, model))
                .Location(l => l.Zone("Content", "5.3").Card(S["Proxy"], "70")));
    }

    private static void Populate(McpResource resource, SftpConnectionFieldsViewModel model)
    {
        PopulateConnectionFields(resource.GetOrCreate<SftpConnectionMetadata>(), model);
    }

    public override async Task<IDisplayResult> UpdateAsync(McpResource resource, UpdateEditorContext context)
    {
        if (resource.Source != SftpResourceConstants.Type)
        {
            return null;
        }

        var model = new SftpConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        resource.Put(ApplyConnectionFields(context, model, resource.GetOrCreate<SftpConnectionMetadata>()));

        return Edit(resource, context);
    }
}
