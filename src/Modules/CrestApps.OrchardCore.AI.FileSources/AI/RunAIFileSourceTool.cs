using System.Text.Json;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.AI.FileSources.AI;

/// <summary>
/// Queues a run of a file source with the run service the Run action of the File Sources page calls. The page runs it
/// during the request; a run can take minutes and spends embedding and vision tokens, so the tool queues it to run after
/// the chat turn, and its summary is stored on the file source as every run's is.
/// </summary>
public sealed class RunAIFileSourceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "runAIFileSource";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the file source to run, from listAIFileSources."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Queues a run of a file source: it reads the folder or the server and ingests new and changed files in the background, after this reply. It connects to the folder or server and spends embedding and vision tokens, and can take minutes on a large folder. Call listAIFileSources with its id later to see how many items were discovered, indexed, removed and failed.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AIModuleTools.IsAuthorizedAsync(services, FileSourcePermissions.ManageFileSources))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was run: give the ID of the file source in id.";
        }

        var fileSource = await services.GetRequiredService<ISourceCatalogManager<FileSource>>().FindByIdAsync(id, cancellationToken);

        if (fileSource is null)
        {
            return $"Nothing was run: no file source has the ID '{id}'. Call listAIFileSources to find it.";
        }

        // Work queued outside a shell scope would never run, so say so instead of claiming it was queued.
        if (ShellScope.Current is null)
        {
            return $"The run of '{fileSource.DisplayText}' could not be queued here. A person can run it with the Run button on the File Sources page: {FileSourceToolSupport.GetIndexPath(services)}";
        }

        var fileSourceId = fileSource.ItemId;

        DeferredWork.Add("ai_file_source_run", scope => RunAsync(scope, fileSourceId));

        return AIModuleTools.Json(new
        {
            fileSource = new
            {
                id = fileSource.ItemId,
                displayText = fileSource.DisplayText,
                connector = fileSource.Source,
            },
            queued = true,
            message = "The run was queued. It starts after this reply, reads the folder or server in the background and has not run yet. Call listAIFileSources with this id later: lastRun shows the items discovered, indexed, removed and failed.",
        });
    }

    private static async Task RunAsync(ShellScope scope, string fileSourceId)
    {
        var services = scope.ServiceProvider;
        var fileSource = await services.GetRequiredService<ISourceCatalogManager<FileSource>>().FindByIdAsync(fileSourceId);

        if (fileSource is null)
        {
            return;
        }

        var summary = await services.GetRequiredService<IIngestionRunService>().RunAsync(fileSource);

        // As the scheduler does after each run, so the run's state is saved even if something later in the scope fails.
        if (services.GetService<IStoreCommitter>() is { } committer)
        {
            await committer.CommitAsync();
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<RunAIFileSourceTool>();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "File source '{FileSourceId}' run from an AI tool saw {Discovered} item(s), ingested {Ingested}, removed {Removed}, failed {Failed}.",
                fileSourceId,
                summary.ItemsDiscovered,
                summary.ItemsIndexed,
                summary.ItemsDeleted,
                summary.ItemsFailed);
        }
    }
}
