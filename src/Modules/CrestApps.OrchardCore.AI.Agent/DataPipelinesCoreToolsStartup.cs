using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.SharedLinks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, revoke and delete shared links, when the Data Pipelines Core feature is on: it hosts
/// the shared links of every module, such as pipeline files and report shares.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataPipelinesCoreToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataPipelinesCoreToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DataPipelinesCoreToolsStartup(IStringLocalizer<DataPipelinesCoreToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListSharedLinksTool>(ListSharedLinksTool.TheName)
            .WithTitle(S["List Shared Links"])
            .WithDescription(S["Lists the shared links of every module with their title, kind, what shared them, recipients, expiry, uses, and whether they still work; optional text filter and paging. It never returns the link's secret URL."])
            .WithCategory(S["Shared Links"])
            .Selectable();

        services.AddCoreAITool<RevokeSharedLinkTool>(RevokeSharedLinkTool.TheName)
            .WithTitle(S["Revoke Shared Link"])
            .WithDescription(S["Stops a shared link from working for all its recipients. The shared file or resource is kept until the link is deleted."])
            .WithCategory(S["Shared Links"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<DeleteSharedLinkTool>(DeleteSharedLinkTool.TheName)
            .WithTitle(S["Delete Shared Link"])
            .WithDescription(S["Deletes a shared link together with the resource it shared, such as the stored copy of an exported file. Cannot be undone."])
            .WithCategory(S["Shared Links"])
            .AsSecurity()
            .Selectable();
    }
}
