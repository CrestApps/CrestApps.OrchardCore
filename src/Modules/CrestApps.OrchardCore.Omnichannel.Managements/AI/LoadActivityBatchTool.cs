using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Starts loading a saved activity load, as the Load action of the Load Activities page does: the load is marked as
/// started by the caller, and the activities are created in the background through the load coordinator.
/// </summary>
public sealed class LoadActivityBatchTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "loadActivityBatch";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the saved load to start, from listActivityBatches. Count it first with previewActivityBatch."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Starts loading a saved batch in the background, creating one activity per match. For an automated or dialer batch this leads to real calls or text messages to customers. A batch can be loaded only once. Call previewActivityBatch first and tell the user how many activities it would create.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was loaded: give the ID of a saved load in id.";
        }

        var manager = services.GetRequiredService<ICatalogManager<OmnichannelActivityBatch>>();
        var batch = await manager.FindByIdAsync(id, cancellationToken);

        if (batch is null)
        {
            return $"Nothing was loaded: no activity load has the ID '{id}'. Call listActivityBatches to find it.";
        }

        switch (ActivityBatchRules.GetLoadRefusal(batch))
        {
            case ActivityBatchLoadRefusal.AlreadyLoaded:
                return $"Nothing was loaded: the load '{batch.DisplayText}' was already loaded and can't be loaded again. Copy it with saveActivityBatch to load the same records again.";

            case ActivityBatchLoadRefusal.Loading:
                return $"Nothing was loaded: the load '{batch.DisplayText}' is already being loaded.";
        }

        // The load runs after this turn, once the batch marked as started is committed. Without a shell scope there is
        // nothing to run it after, so it is not started at all rather than left marked as started.
        if (ShellScope.Current is null)
        {
            return $"The load '{batch.DisplayText}' could not be started here. A person can start it with the Load button on the Load Activities page: {OmnichannelToolSupport.GetPath(services, "ActivityBatches", "Index")}";
        }

        var caller = AIModuleTools.GetCaller(services);
        var loaderId = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        var loaderUserName = caller.Identity?.Name;

        ActivityBatchRules.MarkStarted(batch, loaderId, loaderUserName, services.GetRequiredService<IClock>().UtcNow);
        await manager.UpdateAsync(batch, cancellationToken: cancellationToken);

        var batchId = batch.ItemId;

        var traceParent = TraceContextPropagation.Capture();

        // A deferred task is awaited while the scope that queued it is disposed, which in a chat is the hub call, so a
        // load that takes minutes would hold the user's next message. The deferred task only hands the load to a child
        // scope of its own and returns, as HttpBackgroundJob does, without waiting for a request that, in a hub call,
        // never ends.
        ShellScope.AddDeferredTask(deferredScope =>
        {
            _ = ShellScope.UsingChildScopeAsync(async loadScope =>
            {
                try
                {
                    await DeferredWork.RunAsync(
                        "omnichannel_batch_load",
                        traceParent,
                        loadScope,
                        jobScope => jobScope.ServiceProvider.GetRequiredService<IActivityBatchLoadCoordinator>().LoadAsync(batchId, loaderId, loaderUserName));
                }
                catch (Exception ex)
                {
                    // The load records its own errors on the batch; this only keeps a failure out of an unobserved task.
                    loadScope.ServiceProvider.GetService<ILogger<LoadActivityBatchTool>>()?.LogError(ex, "The activity load '{BatchId}' started from an AI tool failed.", batchId);
                }
            });

            return Task.CompletedTask;
        });

        return AIModuleTools.Json(new
        {
            started = new
            {
                id = batchId,
                displayText = batch.DisplayText,
                status = batch.Status.ToString(),
            },
            note = "The load starts in the background when this reply ends; nothing has been created yet. Check its progress and skipped counts later with listActivityBatches.",
        });
    }
}
