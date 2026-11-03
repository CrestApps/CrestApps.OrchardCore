using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Workflows.Flows;

/// <summary>
/// Gives the AI of a conversation that runs a flow its current stage: the stage's instructions, its ways out, and, in
/// flexible mode, every other stage's instructions. Starts the flow on the conversation's first turn.
/// </summary>
public sealed class AIConversationFlowCompletionHandler : IAICompletionContextBuilderHandler
{
    private readonly AIConversationFlows _flows;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationFlowCompletionHandler"/> class.
    /// </summary>
    /// <param name="flows">Runs the flows.</param>
    /// <param name="logger">The logger.</param>
    public AIConversationFlowCompletionHandler(AIConversationFlows flows, ILogger<AIConversationFlowCompletionHandler> logger)
    {
        _flows = flows;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task BuildingAsync(AICompletionContextBuildingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task BuiltAsync(AICompletionContextBuiltContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Resource is not AIProfile profile || string.IsNullOrEmpty(profile.GetSettings<AIProfileFlowSettings>().WorkflowTypeId))
        {
            return;
        }

        var conversation = AIConversationScope.Resolve();
        var session = conversation?.Session;

        if (session is null)
        {
            return;
        }

        try
        {
            var state = await _flows.StartAsync(profile, session, conversation);
            var stages = profile.GetSettings<AIProfileFlowSettings>().Flexible ? await _flows.GetStagesAsync(profile) : [];
            var instructions = AIConversationFlowPrompt.Build(state, stages);

            if (!string.IsNullOrEmpty(instructions))
            {
                context.Context.SystemMessage = string.IsNullOrEmpty(context.Context.SystemMessage)
                    ? instructions
                    : $"{context.Context.SystemMessage}\n\n{instructions}";
            }
        }
        catch (Exception ex)
        {
            // The conversation goes on without its flow rather than failing.
            _logger.LogError(ex, "The conversation flow of AI profile '{ProfileId}' could not run.", profile.ItemId.SanitizeLogValue());
        }
    }
}

/// <summary>
/// Offers the AI the <c>moveToStage</c> tool while its conversation is in a stage of a flow.
/// </summary>
public sealed class AIConversationFlowToolProvider : IToolRegistryProvider
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(AICompletionContext context, CancellationToken cancellationToken = default)
    {
        var session = AIConversationScope.Resolve()?.Session;

        if (session is null || !session.TryGet<AIConversationFlowState>(out var state) || state.Finished || state.Outcomes.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>([]);
        }

        var outcomes = state.Outcomes.Select(outcome => outcome.Name).ToArray();

        return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(
        [
            new ToolRegistryEntry
            {
                Id = AIConversationFlowPrompt.ToolName,
                Name = AIConversationFlowPrompt.ToolName,
                Description = "Moves the conversation to the next stage of its flow.",
                Source = ToolRegistryEntrySource.System,
                CreateAsync = _ => ValueTask.FromResult<AITool>(new MoveToStageFunction(outcomes)),
            },
        ]);
    }
}

/// <summary>
/// The <c>moveToStage</c> tool: leaves the current stage by one of its outcomes and returns the next stage's
/// instructions.
/// </summary>
public sealed class MoveToStageFunction : AIFunction
{
    private readonly JsonElement _jsonSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoveToStageFunction"/> class.
    /// </summary>
    /// <param name="outcomes">The current stage's ways out.</param>
    public MoveToStageFunction(IReadOnlyList<string> outcomes)
    {
        _jsonSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                outcome = new
                {
                    type = "string",
                    @enum = outcomes,
                    description = "The way out of the current stage that applies.",
                },
            },
            required = new[] { "outcome" },
            additionalProperties = false,
        });
    }

    /// <inheritdoc/>
    public override string Name => AIConversationFlowPrompt.ToolName;

    /// <inheritdoc/>
    public override string Description => "Moves the conversation to the next stage of its flow, by the way out of the current stage that applies. Returns what to do next.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        arguments.TryGetFirstString("outcome", out var outcome);

        var conversation = AIConversationScope.Resolve();
        var flows = arguments.Services?.GetService<AIConversationFlows>();

        if (conversation?.Session is null || flows is null)
        {
            return "This conversation has no flow.";
        }

        var (state, error) = await flows.MoveAsync(conversation.Session.SessionId, outcome);

        if (error is not null)
        {
            return error;
        }

        // The conversation's own copy, so the next turn of this request sees the new stage.
        if (state is not null)
        {
            conversation.Session.Put(state);
        }

        return state is null || state.Finished
            ? "The conversation flow is finished. Wrap up naturally."
            : AIConversationFlowPrompt.Build(state, []);
    }
}
