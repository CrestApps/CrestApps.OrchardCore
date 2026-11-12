using CrestApps.Core;
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Ftp.Models;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.FileTransfer.Core.Drivers;
using CrestApps.OrchardCore.AI.FileTransfer.Core.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using CrestApps.OrchardCore.AI.FileTransfer.Core;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Drivers;

/// <summary>
/// Display driver for the FTP connection of an FTP MCP resource.
/// </summary>
public sealed class FtpResourceDisplayDriver : FtpConnectionDisplayDriverBase<McpResource>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FtpResourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FtpResourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<FtpResourceDisplayDriver> stringLocalizer,
        IOptions<FileTransferHostOptions> hostOptions)
        : base(dataProtectionProvider, stringLocalizer, hostOptions)
    {
    }

    public override IDisplayResult Edit(McpResource resource, BuildEditorContext context)
    {
        if (resource.Source != FtpResourceConstants.Type)
        {
            return null;
        }

        return Initialize<FtpConnectionFieldsViewModel>("FtpResourceConnection_Edit", model =>
        {
            PopulateConnectionFields(resource.GetOrCreate<FtpConnectionMetadata>(), model);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpResource resource, UpdateEditorContext context)
    {
        if (resource.Source != FtpResourceConstants.Type)
        {
            return null;
        }

        var model = new FtpConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        resource.Put(ApplyConnectionFields(context, model, resource.GetOrCreate<FtpConnectionMetadata>()));

        return Edit(resource, context);
    }
}
