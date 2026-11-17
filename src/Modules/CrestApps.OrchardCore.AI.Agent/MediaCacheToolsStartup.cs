using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that purges the local cache of a remote media store, when the Media Cache feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.MediaCache)]
public sealed class MediaCacheToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaCacheToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MediaCacheToolsStartup(IStringLocalizer<MediaCacheToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<PurgeMediaCacheTool>(PurgeMediaCacheTool.TheName)
            .WithTitle(S["Purge Media Cache"])
            .WithDescription(S["Purges the local cache of files fetched from remote media storage (Amazon S3 or Azure Blob); files are fetched again on the next request. Only works when a remote media store is enabled and configured."])
            .WithCategory(S["Media Management"])
            .Selectable();
    }
}
