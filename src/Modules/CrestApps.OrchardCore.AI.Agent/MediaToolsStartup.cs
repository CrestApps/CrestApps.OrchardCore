using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Media;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, read, import, move, copy and delete the files of the media library, when the
/// Media feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Media)]
public sealed class MediaToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MediaToolsStartup(IStringLocalizer<MediaToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // The import tool fetches an address a model chose. Its client follows no redirect by itself and never
        // connects to the site's own network, this machine included.
        services.AddHttpClient(RemoteMediaDownloader.HttpClientName, client => client.DefaultRequestHeaders.UserAgent.ParseAdd(RemoteMediaDownloader.UserAgent))
            .ConfigurePrimaryHttpMessageHandler(() => EgressNetwork.CreateHandler(allowLoopback: false));

        services.AddTransient<RemoteMediaDownloader>();

        services.AddCoreAITool<ListMediaFilesTool>(ListMediaFilesTool.TheName)
            .WithTitle(S["List Media Files"])
            .WithDescription(S["Lists the sub-folders and files of a media folder (the root when no path is given), with optional name and extension filter, recursion and paging; each file has its path, size, MIME type, date and public URL. Use it to find a file's path before using it in content or before moving or deleting it."])
            .WithCategory(S["Media Management"])
            .Selectable();

        services.AddCoreAITool<GetMediaFileTool>(GetMediaFileTool.TheName)
            .WithTitle(S["Get Media File"])
            .WithDescription(S["Returns one media file's details and public URL and, when asked, its text content (text, Markdown, PDF, Word and PowerPoint when the matching media indexing feature is enabled; small plain-text files otherwise). Use it to check that a file exists or to read a document stored in the library."])
            .WithCategory(S["Media Management"])
            .Selectable();

        services.AddCoreAITool<ImportMediaFromUrlTool>(ImportMediaFromUrlTool.TheName)
            .WithTitle(S["Import Media From URL"])
            .WithDescription(S["Downloads a file from a public http(s) URL and stores it in a media folder, returning its media path and public URL. Use it when the user gives a link to an image or document; the extension must be allowed, the size within the upload limit, and an existing file is only replaced when `overwrite` is set."])
            .WithCategory(S["Media Management"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<MoveMediaFilesTool>(MoveMediaFilesTool.TheName)
            .WithTitle(S["Move Media Files"])
            .WithDescription(S["Moves, renames or copies media files: give the action (move or copy) and each file's current path and new path (same folder with a new name to rename). A move does not update content that references the old path, so report the references that need fixing. Fails when a target exists, its folder does not exist or the new extension is not allowed."])
            .WithCategory(S["Media Management"])
            .Selectable();

        services.AddCoreAITool<DeleteMediaTool>(DeleteMediaTool.TheName)
            .WithTitle(S["Delete Media"])
            .WithDescription(S["Permanently deletes one or more media files by path. There is no recycle bin and content that references the files will show broken media, so confirm the exact paths first. Folders cannot be deleted with this tool; delete them in the Media Library."])
            .WithCategory(S["Media Management"])
            .AsDestructive()
            .Selectable();
    }
}
