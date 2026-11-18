using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.FileSources.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.FileSources;

/// <summary>
/// Registers the AI tools that list, save, delete and run file sources. No tool returns or takes a password, key or
/// passphrase. Saving, deleting and running a file source re-embeds content, so those tools ask for approval.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAIFileSourcesTool>(ListAIFileSourcesTool.TheName)
            .WithTitle(S["List AI File Sources"])
            .WithDescription(S["Lists the file sources that feed File AI data sources, with connector, data source, enabled state, schedule and connector settings, with optional name search or one file source by id. Also returns the connectors enabled on this site. Passwords and keys are never returned."])
            .WithCategory(S["AI Knowledge"])
            .Selectable();

        services.AddCoreAITool<SaveAIFileSourceTool>(SaveAIFileSourceTool.TheName)
            .WithTitle(S["Save AI File Source"])
            .WithDescription(S["Creates a file source for a connector (file system or FTP), or updates one by id. It needs an existing File AI data source; a file system path can only be inside the tenant's own file-sources folder. Passwords and keys are entered on the admin page."])
            .WithCategory(S["AI Knowledge"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteAIFileSourceTool>(DeleteAIFileSourceTool.TheName)
            .WithTitle(S["Delete AI File Source"])
            .WithDescription(S["Deletes a file source and queues the removal of everything it ingested from the knowledge base."])
            .WithCategory(S["AI Knowledge"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<RunAIFileSourceTool>(RunAIFileSourceTool.TheName)
            .WithTitle(S["Run AI File Source"])
            .WithDescription(S["Queues a run that reads a file source's folder and ingests new and changed files in the background. It connects to the folder or server and spends embedding and vision tokens, and can take minutes on a large folder."])
            .WithCategory(S["AI Knowledge"])
            .RequireApproval()
            .Selectable();
    }
}
