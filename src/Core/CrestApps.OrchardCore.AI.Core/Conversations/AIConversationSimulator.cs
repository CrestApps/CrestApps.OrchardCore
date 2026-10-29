using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// A simulated test conversation to run.
/// </summary>
public sealed class AIConversationSimulationRequest
{
    /// <summary>
    /// Gets or sets the AI profile under test.
    /// </summary>
    public AIProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets who the simulated customer is, what they want and how they behave.
    /// </summary>
    public string Scenario { get; set; }

    /// <summary>
    /// Gets or sets the conversation variables the conversation starts with.
    /// </summary>
    public IReadOnlyDictionary<string, string> Variables { get; set; }

    /// <summary>
    /// Gets or sets the most customer turns before the simulation stops. Defaults to 8.
    /// </summary>
    public int MaxTurns { get; set; } = AIConversationSimulator.DefaultMaxTurns;

    /// <summary>
    /// Gets or sets the deployment that plays the customer, or <see langword="null"/> for the utility deployment.
    /// </summary>
    public string CustomerDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the result each named tool returns instead of running, so a test does not book, charge or send
    /// anything for real.
    /// </summary>
    public IReadOnlyDictionary<string, string> MockedTools { get; set; }
}

/// <summary>
/// How a simulated conversation went.
/// </summary>
public sealed class AIConversationSimulationResult
{
    /// <summary>
    /// Gets the turns, oldest first.
    /// </summary>
    public List<AIConversationTurn> Turns { get; } = [];

    /// <summary>
    /// Gets or sets whether the customer ended the conversation, rather than it reaching the turn limit.
    /// </summary>
    public bool CustomerEnded { get; set; }

    /// <summary>
    /// Gets or sets the instructions the AI had, after its variables were rendered in.
    /// </summary>
    public string Instructions { get; set; }
}

/// <summary>
/// Plays an AI profile against a customer played by a model, in text, through the same completion pipeline a
/// conversation uses: the profile's instructions, variables, knowledge and tools. Tool workflows run as usual and
/// read <c>Workflow.Input.Simulation</c> to answer with test data instead of acting.
/// </summary>
public sealed class AIConversationSimulator
{
    /// <summary>
    /// The turn limit when none is given.
    /// </summary>
    public const int DefaultMaxTurns = 8;

    /// <summary>
    /// What the simulated customer says to end the conversation.
    /// </summary>
    internal const string EndMarker = "[END]";

    private readonly IAICompletionContextBuilder _contextBuilder;
    private readonly IAICompletionService _completionService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;
    private readonly IToolRegistry _toolRegistry;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationSimulator"/> class.
    /// </summary>
    /// <param name="contextBuilder">The completion context builder.</param>
    /// <param name="completionService">The completion service the AI answers through.</param>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="clientFactory">The AI client factory.</param>
    /// <param name="toolRegistry">The tool registry, which gives the profile's tools.</param>
    /// <param name="clock">The clock.</param>
    public AIConversationSimulator(
        IAICompletionContextBuilder contextBuilder,
        IAICompletionService completionService,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory,
        IToolRegistry toolRegistry,
        IClock clock)
    {
        _contextBuilder = contextBuilder;
        _completionService = completionService;
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
        _toolRegistry = toolRegistry;
        _clock = clock;
    }

    /// <summary>
    /// Runs the conversation.
    /// </summary>
    /// <param name="request">The conversation to run.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="InvalidOperationException">No deployment can answer for the profile or play the customer.</exception>
    public async Task<AIConversationSimulationResult> SimulateAsync(AIConversationSimulationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Profile);

        var conversation = new AIConversationContext
        {
            Channel = AIConversationChannels.Chat,
            Direction = AIConversationDirections.Inbound,
            Profile = request.Profile,
            IsSimulation = true,
        };

        foreach (var (name, value) in request.Variables ?? new Dictionary<string, string>())
        {
            conversation.InitialVariables[name] = value;
        }

        using var conversationScope = AIConversationScope.Begin(conversation);
        using var usageScope = AIUsageScope.Begin(contextType: AIUsageCategories.Workflow, purpose: AIUsageFeaturePurposes.ConversationSimulation);
        using var invocationScope = AIInvocationScope.Begin();
        invocationScope.Context.ToolExecutionContext = new AIToolExecutionContext(request.Profile);

        var context = await _contextBuilder.BuildAsync(request.Profile, cancellationToken: cancellationToken);
        invocationScope.Context.CompletionContext = context;

        // The completion service is called directly rather than through the orchestrator, so the profile's tools
        // are attached here or the AI under test sees none.
        await AIConversationTools.AttachAsync(_toolRegistry, context, [], cancellationToken);
        AIConversationTools.Mock(context, request.MockedTools);
        var assistantDeployment = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, context.ChatDeploymentName, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"No chat deployment can answer for AI profile '{request.Profile.ItemId}'.");

        var customerDeployment = await _deploymentManager.ResolveUtilityOrDefaultAsync(utilityDeploymentName: request.CustomerDeploymentName)
            ?? throw new InvalidOperationException("No deployment can play the simulated customer.");

        var customer = await _clientFactory.CreateChatClientAsync(customerDeployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: AIUsageFeaturePurposes.ConversationSimulation));

        var result = new AIConversationSimulationResult
        {
            Instructions = context.SystemMessage,
        };

        var maxTurns = Math.Clamp(request.MaxTurns, 1, 50);

        for (var turn = 0; turn < maxTurns; turn++)
        {
            var customerResponse = await customer.GetResponseAsync(BuildCustomerMessages(request.Scenario, result.Turns), cancellationToken: cancellationToken);
            var (said, ended) = ReadCustomerLine(customerResponse.Text);

            if (said.Length > 0)
            {
                result.Turns.Add(new AIConversationTurn(AIConversationTurn.CustomerRole, said, _clock.UtcNow));
            }

            if (ended || said.Length == 0)
            {
                result.CustomerEnded = true;

                break;
            }

            var answer = await _completionService.CompleteAsync(assistantDeployment, BuildAssistantMessages(result.Turns), context, cancellationToken);
            var reply = answer?.Text?.Trim();

            result.Turns.Add(new AIConversationTurn(AIConversationTurn.AssistantRole, string.IsNullOrEmpty(reply) ? "(no reply)" : reply, _clock.UtcNow));
        }

        return result;
    }

    /// <summary>
    /// The conversation as the AI under test sees it.
    /// </summary>
    /// <param name="turns">The turns so far.</param>
    internal static List<ChatMessage> BuildAssistantMessages(IEnumerable<AIConversationTurn> turns)
        => turns
            .Select(turn => new ChatMessage(turn.Role == AIConversationTurn.CustomerRole ? ChatRole.User : ChatRole.Assistant, turn.Text))
            .ToList();

    /// <summary>
    /// The conversation as the simulated customer sees it: the scenario as its instructions, and the roles turned
    /// around so the AI's lines are what it answers.
    /// </summary>
    /// <param name="scenario">Who the customer is and what they want.</param>
    /// <param name="turns">The turns so far.</param>
    internal static List<ChatMessage> BuildCustomerMessages(string scenario, IEnumerable<AIConversationTurn> turns)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System,
                $"""
                You are role-playing a customer talking to a business's assistant, to test the assistant.
                Stay in character and never say you are an AI or that this is a test.
                Write only what the customer says next: one short, natural message, the way a real person would
                write or speak it. Do not narrate.
                When your goal is met, the assistant cannot help, or the conversation has naturally ended, reply
                with exactly {EndMarker}.

                Who you are and what you want:
                {(string.IsNullOrWhiteSpace(scenario) ? "A customer with a simple question." : scenario.Trim())}
                """),
        };

        var any = false;

        foreach (var turn in turns)
        {
            any = true;
            messages.Add(new ChatMessage(turn.Role == AIConversationTurn.CustomerRole ? ChatRole.Assistant : ChatRole.User, turn.Text));
        }

        if (!any)
        {
            messages.Add(new ChatMessage(ChatRole.User, "(The assistant is waiting for you to start. Say your first message.)"));
        }

        return messages;
    }

    /// <summary>
    /// Reads what the simulated customer said, and whether it ended the conversation.
    /// </summary>
    /// <param name="text">The model's answer.</param>
    internal static (string Said, bool Ended) ReadCustomerLine(string text)
    {
        var said = (text ?? string.Empty).Trim();
        var ended = said.Contains(EndMarker, StringComparison.OrdinalIgnoreCase);

        if (ended)
        {
            said = said.Replace(EndMarker, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        }

        // A role-play model sometimes prefixes its own name.
        if (said.StartsWith("Customer:", StringComparison.OrdinalIgnoreCase))
        {
            said = said["Customer:".Length..].Trim();
        }

        return (said, ended);
    }
}
