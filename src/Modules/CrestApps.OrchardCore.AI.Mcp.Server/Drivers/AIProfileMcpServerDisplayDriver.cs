using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Server.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Server.Drivers;

internal sealed class AIProfileMcpServerDisplayDriver : DisplayDriver<AIProfile>
{
    internal readonly IStringLocalizer S;

    public AIProfileMcpServerDisplayDriver(
        IStringLocalizer<AIProfileMcpServerDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<McpServerMetadataViewModel>("EditProfileMcpServer_Edit", model =>
        {
            var mcpMetadata = profile.As<McpServerMetadata>();

            model.UseLocalServer = mcpMetadata.UseLocalServer;

        }).Location("Content:8.1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new McpServerMetadataViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.Alter<McpServerMetadata>(part =>
        {
            part.UseLocalServer = model.UseLocalServer;
        });

        return Edit(profile, context);
    }
}

