using System.Text.Json;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Asks a model to rewrite a builder session's instructions from feedback, and to judge replayed replies.
/// </summary>
public sealed class ProfileBuilderCoach
{
    internal const string CoachTemplateId = "profile-builder-coach";
    internal const string JudgeTemplateId = "profile-builder-replay-judge";

    // Read-only options with a type resolver, which structured output needs; the property names are camel case
    // to match the names the prompts ask for.
    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;
    private readonly ITemplateService _templateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileBuilderCoach"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="clientFactory">The AI client factory.</param>
    /// <param name="templateService">The prompt template service.</param>
    public ProfileBuilderCoach(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory,
        ITemplateService templateService)
    {
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
        _templateService = templateService;
    }

    /// <summary>
    /// Resolves the deployment that coaches a session: the session's own choice, then the interaction's chat
    /// deployment, then the site's default chat deployment.
    /// </summary>
    /// <param name="session">The builder session.</param>
    /// <param name="interaction">The session's chat interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async ValueTask<AIDeployment> ResolveDeploymentAsync(ProfileBuilderSession session, ChatInteraction interaction, CancellationToken cancellationToken = default)
    {
        var deploymentName = !string.IsNullOrWhiteSpace(session.CoachDeploymentName)
            ? session.CoachDeploymentName
            : interaction.ChatDeploymentName;

        return await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asks the coach for new settings that apply <paramref name="newFeedback"/> without undoing earlier fixes.
    /// </summary>
    /// <returns>The coach's answer, or <see langword="null"/> when no deployment can coach.</returns>
    public async Task<ProfileBuilderCoachResult> RewriteAsync(
        ProfileBuilderSession session,
        ChatInteraction interaction,
        ProfileBuilderSettings current,
        IReadOnlyList<ProfileBuilderTurn> transcript,
        ProfileBuilderFeedback newFeedback,
        CancellationToken cancellationToken = default)
    {
        var deployment = await ResolveDeploymentAsync(session, interaction, cancellationToken);

        if (deployment is null)
        {
            return null;
        }

        var systemPrompt = await _templateService.RenderAsync(CoachTemplateId, cancellationToken: cancellationToken);
        var userPrompt = ProfileBuilderCoachPrompt.BuildCoachRequest(current, transcript, session.Feedback, session.Revisions, newFeedback);

        var client = await _clientFactory.CreateChatClientAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: AIUsageFeaturePurposes.ProfileBuilderCoaching));

        var response = await client.GetResponseAsync<ProfileBuilderCoachResult>(
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt),
            ],
            _jsonOptions,
            cancellationToken: cancellationToken);

        return response.Result;
    }

    /// <summary>
    /// Asks the judge whether each replayed reply honours the feedback given on that situation.
    /// </summary>
    /// <returns>The verdicts keyed by case identifier; empty when no deployment can judge.</returns>
    public async Task<IReadOnlyDictionary<string, ProfileBuilderJudgeVerdict>> JudgeAsync(
        ProfileBuilderSession session,
        ChatInteraction interaction,
        ProfileBuilderSettings current,
        IReadOnlyList<(string CaseId, string CustomerMessage, string Reply, IReadOnlyList<ProfileBuilderFeedback> Feedback)> cases,
        CancellationToken cancellationToken = default)
    {
        var verdicts = new Dictionary<string, ProfileBuilderJudgeVerdict>(StringComparer.Ordinal);

        if (cases.Count == 0)
        {
            return verdicts;
        }

        var deployment = await ResolveDeploymentAsync(session, interaction, cancellationToken);

        if (deployment is null)
        {
            return verdicts;
        }

        var systemPrompt = await _templateService.RenderAsync(JudgeTemplateId, cancellationToken: cancellationToken);
        var userPrompt = ProfileBuilderCoachPrompt.BuildJudgeRequest(current, cases);

        var client = await _clientFactory.CreateChatClientAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: AIUsageFeaturePurposes.ProfileBuilderReplay));

        var response = await client.GetResponseAsync<ProfileBuilderJudgeResult>(
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt),
            ],
            _jsonOptions,
            cancellationToken: cancellationToken);

        foreach (var verdict in response.Result?.Cases ?? [])
        {
            if (!string.IsNullOrWhiteSpace(verdict?.CaseId))
            {
                verdicts[verdict.CaseId.Trim()] = verdict;
            }
        }

        return verdicts;
    }
}

/// <summary>
/// The settings the coach proposes. A <see langword="null"/> value keeps the current one.
/// </summary>
public sealed class ProfileBuilderCoachResult
{
    public string SystemMessage { get; set; }

    public string InitialPrompt { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public int? MaxTokens { get; set; }

    public int? PastMessagesCount { get; set; }

    public string Summary { get; set; }

    /// <summary>
    /// Applies this answer to <paramref name="current"/> and returns the resulting settings. Values outside the
    /// ranges the settings panel accepts are clamped, and an empty system message keeps the current one.
    /// </summary>
    /// <param name="current">The settings the coach was given.</param>
    public ProfileBuilderSettings ApplyTo(ProfileBuilderSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);

        var settings = current.Clone();

        if (!string.IsNullOrWhiteSpace(SystemMessage))
        {
            settings.SystemMessage = SystemMessage.Trim();
        }

        if (!string.IsNullOrWhiteSpace(InitialPrompt))
        {
            settings.InitialPrompt = InitialPrompt.Trim();
        }

        settings.Temperature = Clamp(Temperature, 0, 2) ?? settings.Temperature;
        settings.TopP = Clamp(TopP, 0, 1) ?? settings.TopP;
        settings.FrequencyPenalty = Clamp(FrequencyPenalty, 0, 2) ?? settings.FrequencyPenalty;
        settings.PresencePenalty = Clamp(PresencePenalty, 0, 2) ?? settings.PresencePenalty;
        settings.MaxTokens = MaxTokens.HasValue ? Math.Max(4, MaxTokens.Value) : settings.MaxTokens;
        settings.PastMessagesCount = PastMessagesCount.HasValue ? Math.Clamp(PastMessagesCount.Value, 2, 50) : settings.PastMessagesCount;

        return settings;
    }

    private static float? Clamp(float? value, float min, float max)
        => value.HasValue && !float.IsNaN(value.Value) ? Math.Clamp(value.Value, min, max) : null;
}

/// <summary>
/// The judge's answer for a replay.
/// </summary>
public sealed class ProfileBuilderJudgeResult
{
    public List<ProfileBuilderJudgeVerdict> Cases { get; set; } = [];
}

/// <summary>
/// The judge's verdict on one replayed case.
/// </summary>
public sealed class ProfileBuilderJudgeVerdict
{
    public string CaseId { get; set; }

    public string Verdict { get; set; }

    public string Reason { get; set; }

    /// <summary>
    /// Reads <see cref="Verdict"/> leniently, treating anything unrecognised as unknown.
    /// </summary>
    public ProfileBuilderReplayVerdict ToVerdict()
        => Enum.TryParse<ProfileBuilderReplayVerdict>(Verdict?.Trim(), ignoreCase: true, out var verdict)
            ? verdict
            : ProfileBuilderReplayVerdict.Unknown;
}
