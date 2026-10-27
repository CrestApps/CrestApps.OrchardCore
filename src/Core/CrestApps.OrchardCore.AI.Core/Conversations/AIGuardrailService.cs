using System.Text;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Resilience;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Which side of the conversation a guardrail checked.
/// </summary>
public enum AIGuardrailDirection
{
    /// <summary>
    /// What the customer said, before the AI answers it.
    /// </summary>
    Customer,

    /// <summary>
    /// The AI's reply, before it is sent or spoken.
    /// </summary>
    Reply,
}

/// <summary>
/// A guardrail that triggered.
/// </summary>
public sealed class AIGuardrailVerdict
{
    /// <summary>
    /// Gets or sets the AI profile.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the chat session, when the conversation has one.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets which side was checked.
    /// </summary>
    public AIGuardrailDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the category, from <see cref="AIGuardrailCategories"/>.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the utility model's one-sentence reason.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the text that was checked.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets what the AI says instead.
    /// </summary>
    public string Reply { get; set; }
}

/// <summary>
/// Called when a guardrail triggers. Workflows take part through the <c>AI Guardrail Triggered</c> event.
/// </summary>
public interface IAIGuardrailHandler
{
    /// <summary>
    /// Called after a guardrail triggered, before the placeholder reply is sent.
    /// </summary>
    /// <param name="verdict">The guardrail that triggered.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task TriggeredAsync(AIGuardrailVerdict verdict, CancellationToken cancellationToken);
}

/// <summary>
/// Checks what the customer says, and the AI's replies, against the topics an AI profile refuses, with the utility
/// model. Used by web chat, text messages and turn-based calls.
/// </summary>
public sealed class AIGuardrailService
{
    /// <summary>
    /// What the AI says when a guardrail triggers and the profile does not say otherwise.
    /// </summary>
    public const string DefaultPlaceholderReply = "I'm sorry, I can't help with that. Is there something else I can help you with?";

    private const int MaxCheckedLength = 4000;

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;
    private readonly IEnumerable<IAIGuardrailHandler> _handlers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIGuardrailService"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager the utility model is resolved from.</param>
    /// <param name="clientFactory">The AI client factory.</param>
    /// <param name="handlers">The handlers told when a guardrail triggers.</param>
    /// <param name="logger">The logger.</param>
    public AIGuardrailService(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory,
        IEnumerable<IAIGuardrailHandler> handlers,
        ILogger<AIGuardrailService> logger)
    {
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
        _handlers = handlers;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether a profile checks this side of the conversation.
    /// </summary>
    /// <param name="profile">The AI profile.</param>
    /// <param name="direction">The side.</param>
    public static bool IsOn(AIProfile profile, AIGuardrailDirection direction)
    {
        if (profile is null || !profile.TryGetSettings<AIProfileGuardrailSettings>(out var settings) || !settings.HasTopics)
        {
            return false;
        }

        return direction == AIGuardrailDirection.Customer ? settings.CheckCustomer : settings.CheckReplies;
    }

    /// <summary>
    /// Gets what the AI says when a guardrail of the profile triggers.
    /// </summary>
    /// <param name="profile">The AI profile.</param>
    public static string GetPlaceholderReply(AIProfile profile)
    {
        var reply = profile?.GetSettings<AIProfileGuardrailSettings>()?.PlaceholderReply;

        return string.IsNullOrWhiteSpace(reply) ? DefaultPlaceholderReply : reply.Trim();
    }

    /// <summary>
    /// Checks a text against the profile's guardrails.
    /// </summary>
    /// <param name="profile">The AI profile.</param>
    /// <param name="text">What the customer said, or the AI's reply.</param>
    /// <param name="direction">Which of the two it is.</param>
    /// <param name="sessionId">The chat session, when the conversation has one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The guardrail that triggered, or <see langword="null"/> when the text may go on.</returns>
    public async Task<AIGuardrailVerdict> CheckAsync(AIProfile profile, string text, AIGuardrailDirection direction, string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || !IsOn(profile, direction))
        {
            return null;
        }

        var settings = profile.GetSettings<AIProfileGuardrailSettings>();
        AIGuardrailJudgement judgement;

        try
        {
            var deployment = await _deploymentManager.ResolveUtilityOrDefaultAsync();

            if (deployment is null)
            {
                return null;
            }

            var client = await _clientFactory.CreateChatClientAsync(deployment, builder => builder
                .UseDefaultResilience()
                .UseUsageLabels(purpose: AIUsageFeaturePurposes.Guardrail));

            var response = await client.GetResponseAsync<AIGuardrailJudgement>(
                [
                    new ChatMessage(ChatRole.System, SystemPrompt),
                    new ChatMessage(ChatRole.User, BuildRequest(settings, text, direction)),
                ],
                _jsonOptions,
                cancellationToken: cancellationToken);

            judgement = response.Result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The conversation goes on unchecked rather than failing: a guardrail is a second line behind the
            // instructions and Prompt Security, and a customer left without an answer is worse than one unchecked turn.
            _logger.LogWarning(ex, "The guardrails of AI profile '{ProfileId}' could not be checked.", profile.ItemId);

            return null;
        }

        var verdict = ToVerdict(judgement, settings, profile, text, direction, sessionId);

        if (verdict is null)
        {
            return null;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "A guardrail of AI profile '{ProfileId}' triggered on the {Direction} in session '{SessionId}': {Category}.",
                profile.ItemId,
                direction,
                sessionId,
                verdict.Category);
        }

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.TriggeredAsync(verdict, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A guardrail handler failed for AI profile '{ProfileId}'.", profile.ItemId);
            }
        }

        return verdict;
    }

    internal const string SystemPrompt =
        """
        You are a content moderator for a customer service assistant. You are given a message and the topics the
        business does not allow. Decide whether the message falls under one of those topics. Judge what the message
        actually asks for or says, not words taken out of context: a customer asking about a refund for a damaged
        knife is not violence, and "this is killing me" about a late order is not self-harm. Ordinary questions about
        the business's products, orders, accounts and policies are always allowed.
        Answer only with JSON: {"violates": false, "category": "", "reason": ""}, where category is the identifier of
        the topic shown in brackets and reason is one short sentence.
        """;

    internal static string BuildRequest(AIProfileGuardrailSettings settings, string text, AIGuardrailDirection direction)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Topics not allowed:");

        foreach (var category in AIGuardrailCategories.Normalize(settings.Categories))
        {
            builder.Append("- [").Append(category).Append("] ").AppendLine(AIGuardrailCategories.Definitions[category]);
        }

        if (!string.IsNullOrWhiteSpace(settings.OtherTopics))
        {
            builder.Append("- [").Append(AIGuardrailCategories.Other).Append("] ").AppendLine(settings.OtherTopics.Trim());
        }

        builder.AppendLine();
        builder.AppendLine(direction == AIGuardrailDirection.Customer
            ? "The customer's message:"
            : "The assistant's reply, which must not provide or engage with a topic that is not allowed:");

        builder.AppendLine(text.Length > MaxCheckedLength ? text[..MaxCheckedLength] : text);

        return builder.ToString();
    }

    internal static AIGuardrailVerdict ToVerdict(AIGuardrailJudgement judgement, AIProfileGuardrailSettings settings, AIProfile profile, string text, AIGuardrailDirection direction, string sessionId)
    {
        if (judgement is null || !judgement.Violates)
        {
            return null;
        }

        var chosen = AIGuardrailCategories.Normalize(settings.Categories);
        var category = chosen.FirstOrDefault(category => string.Equals(category, judgement.Category?.Trim().Trim('[', ']'), StringComparison.OrdinalIgnoreCase));

        if (category is null)
        {
            // The model named a topic the profile does not refuse. Only the profile's own description of other
            // topics can account for that; without one the message is allowed, so a guardrail never refuses more
            // than it was set to.
            if (string.IsNullOrWhiteSpace(settings.OtherTopics))
            {
                return null;
            }

            category = AIGuardrailCategories.Other;
        }

        return new AIGuardrailVerdict
        {
            ProfileId = profile.ItemId,
            SessionId = sessionId,
            Direction = direction,
            Category = category,
            Reason = judgement.Reason?.Trim(),
            Text = text,
            Reply = GetPlaceholderReply(profile),
        };
    }
}

/// <summary>
/// The utility model's answer to a guardrail check.
/// </summary>
public sealed class AIGuardrailJudgement
{
    /// <summary>
    /// Gets or sets whether the message falls under a topic that is not allowed.
    /// </summary>
    public bool Violates { get; set; }

    /// <summary>
    /// Gets or sets the topic.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets why, in a sentence.
    /// </summary>
    public string Reason { get; set; }
}
