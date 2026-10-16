using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Runs an AI Profile Builder session: records feedback, has the coach rewrite the instructions, removes rejected
/// replies so they can be generated again, restores revisions, and replays earlier feedback to catch regressions.
/// </summary>
public sealed class ProfileBuilderService
{
    /// <summary>
    /// The most situations one replay answers again, so a long session cannot turn a replay into a runaway bill.
    /// </summary>
    internal const int MaxReplayCases = 20;

    private readonly ICatalogManager<ChatInteraction> _interactionManager;
    private readonly IChatInteractionPromptStore _promptStore;
    private readonly IProfileBuilderSessionStore _sessionStore;
    private readonly ProfileBuilderCoach _coach;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAICompletionService _completionService;
    private readonly IAICompletionContextBuilder _completionContextBuilder;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileBuilderService"/> class.
    /// </summary>
    public ProfileBuilderService(
        ICatalogManager<ChatInteraction> interactionManager,
        IChatInteractionPromptStore promptStore,
        IProfileBuilderSessionStore sessionStore,
        ProfileBuilderCoach coach,
        IAIDeploymentManager deploymentManager,
        IAICompletionService completionService,
        IAICompletionContextBuilder completionContextBuilder,
        IClock clock,
        ILogger<ProfileBuilderService> logger)
    {
        _interactionManager = interactionManager;
        _promptStore = promptStore;
        _sessionStore = sessionStore;
        _coach = coach;
        _deploymentManager = deploymentManager;
        _completionService = completionService;
        _completionContextBuilder = completionContextBuilder;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Creates a session and the chat interaction that carries its conversation. When <paramref name="sourceProfile"/>
    /// is given, the session starts from that profile's settings and remembers it, so it can be updated at the end.
    /// </summary>
    /// <param name="ownerId">The identifier of the user who owns the session.</param>
    /// <param name="author">The name of the user who owns the session.</param>
    /// <param name="title">The title of the session.</param>
    /// <param name="sourceProfile">The profile to start from, or <see langword="null"/> to start empty.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<(ProfileBuilderSession Session, ChatInteraction Interaction)> CreateAsync(
        string ownerId,
        string author,
        string title,
        AIProfile sourceProfile = null,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var interaction = await _interactionManager.NewAsync(cancellationToken: cancellationToken);

        var session = new ProfileBuilderSession
        {
            ItemId = IdGenerator.GenerateId(),
            ChatInteractionId = interaction.ItemId,
            OwnerId = ownerId,
            Author = author,
            CreatedUtc = now,
        };

        if (sourceProfile is not null)
        {
            ProfileBuilderProfileMapper.CopyFromProfile(sourceProfile, interaction, session);
        }

        interaction.Title = string.IsNullOrWhiteSpace(title)
            ? sourceProfile?.DisplayText ?? sourceProfile?.Name
            : title.Trim();
        interaction.OwnerId ??= ownerId;
        interaction.Author ??= author;
        interaction.Put(new ProfileBuilderInteractionMetadata { SessionId = session.ItemId });

        await _interactionManager.CreateAsync(interaction, cancellationToken);

        session.Revisions.Add(new ProfileBuilderRevision
        {
            Number = 1,
            Source = ProfileBuilderRevisionSource.Initial,
            Summary = sourceProfile is null ? "Started a new session." : $"Started from the AI profile '{sourceProfile.DisplayText ?? sourceProfile.Name}'.",
            Settings = ReadSettings(interaction, session),
            CreatedUtc = now,
        });

        await _sessionStore.CreateAsync(session, cancellationToken);

        return (session, interaction);
    }

    /// <summary>
    /// Reads the values the builder tunes from the interaction and the session.
    /// </summary>
    public static ProfileBuilderSettings ReadSettings(ChatInteraction interaction, ProfileBuilderSession session)
        => new()
        {
            SystemMessage = interaction.SystemMessage,
            InitialPrompt = session.InitialPrompt,
            Temperature = interaction.Temperature,
            TopP = interaction.TopP,
            FrequencyPenalty = interaction.FrequencyPenalty,
            PresencePenalty = interaction.PresencePenalty,
            MaxTokens = interaction.MaxTokens,
            PastMessagesCount = interaction.PastMessagesCount,
        };

    /// <summary>
    /// Writes the values the builder tunes onto the interaction and the session.
    /// </summary>
    public static void ApplySettings(ProfileBuilderSettings settings, ChatInteraction interaction, ProfileBuilderSession session)
    {
        interaction.SystemMessage = settings.SystemMessage;
        interaction.Temperature = settings.Temperature;
        interaction.TopP = settings.TopP;
        interaction.FrequencyPenalty = settings.FrequencyPenalty;
        interaction.PresencePenalty = settings.PresencePenalty;
        interaction.MaxTokens = settings.MaxTokens;
        interaction.PastMessagesCount = settings.PastMessagesCount;
        session.InitialPrompt = settings.InitialPrompt;
    }

    /// <summary>
    /// Records the settings as a manual revision when the author changed them in the settings panel since the last
    /// revision, so the history and a later restore never lose an edit the author made by hand.
    /// </summary>
    /// <returns><see langword="true"/> when a revision was added.</returns>
    public bool CaptureManualChanges(ProfileBuilderSession session, ChatInteraction interaction)
    {
        var current = ReadSettings(interaction, session);
        var latest = session.Revisions.LastOrDefault();

        if (latest is not null && latest.Settings.HasSameValues(current))
        {
            return false;
        }

        session.Revisions.Add(new ProfileBuilderRevision
        {
            Number = NextRevisionNumber(session),
            Source = latest is null ? ProfileBuilderRevisionSource.Initial : ProfileBuilderRevisionSource.Manual,
            Summary = latest is null ? "Started a new session." : "Edited in the settings panel.",
            Settings = current,
            CreatedUtc = _clock.UtcNow,
        });

        return true;
    }

    /// <summary>
    /// Gets the conversation as stored, oldest first, leaving out generated prompts.
    /// </summary>
    public async Task<IReadOnlyList<ChatInteractionPrompt>> GetConversationAsync(string interactionId)
    {
        var prompts = await _promptStore.GetPromptsAsync(interactionId);

        return OrderConversation(prompts);
    }

    /// <summary>
    /// Orders prompts oldest first. A customer message and the reply to it share a timestamp, so the customer
    /// message is placed first within one.
    /// </summary>
    internal static IReadOnlyList<ChatInteractionPrompt> OrderConversation(IEnumerable<ChatInteractionPrompt> prompts)
        => prompts
            .Where(prompt => !prompt.IsGeneratedPrompt && (prompt.Role == ChatRole.User || prompt.Role == ChatRole.Assistant))
            .Select((prompt, index) => (prompt, index))
            .OrderBy(entry => entry.prompt.CreatedUtc)
            .ThenBy(entry => entry.prompt.Role == ChatRole.Assistant ? 1 : 0)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.prompt)
            .ToArray();

    /// <summary>
    /// Finds the assistant prompt feedback is about: by identifier when the client knows it, otherwise by the reply's
    /// text, otherwise by its position among the assistant replies.
    /// </summary>
    internal static ChatInteractionPrompt FindAssistantPrompt(IReadOnlyList<ChatInteractionPrompt> conversation, string promptId, string replyText, int? assistantIndex)
    {
        var replies = conversation.Where(prompt => prompt.Role == ChatRole.Assistant).ToArray();

        if (!string.IsNullOrEmpty(promptId))
        {
            var byId = replies.FirstOrDefault(prompt => string.Equals(prompt.ItemId, promptId, StringComparison.Ordinal));

            if (byId is not null)
            {
                return byId;
            }
        }

        var normalized = NormalizeText(replyText);

        if (normalized.Length > 0)
        {
            var matches = replies
                .Select((prompt, index) => (prompt, index))
                .Where(entry => NormalizeText(entry.prompt.Text) == normalized)
                .ToArray();

            if (matches.Length == 1)
            {
                return matches[0].prompt;
            }

            if (matches.Length > 1)
            {
                var target = assistantIndex ?? int.MaxValue;

                return matches.OrderBy(entry => Math.Abs(entry.index - target)).First().prompt;
            }
        }

        if (assistantIndex is >= 0 && assistantIndex < replies.Length)
        {
            return replies[assistantIndex.Value];
        }

        return null;
    }

    /// <summary>
    /// Records feedback on a reply and, unless it is an approval, has the coach rewrite the settings from it.
    /// </summary>
    public async Task<ProfileBuilderFeedbackOutcome> SubmitFeedbackAsync(
        ProfileBuilderSession session,
        ChatInteraction interaction,
        ProfileBuilderFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversation = await GetConversationAsync(interaction.ItemId);
        var reply = FindAssistantPrompt(conversation, request.PromptId, request.ReplyText, request.AssistantIndex);

        if (reply is null)
        {
            return ProfileBuilderFeedbackOutcome.Failed("The reply could not be found. Reload the page and try again.");
        }

        // Approving a reply that is already approved, for example by a second click, records nothing new.
        var latestOnReply = session.Feedback.LastOrDefault(entry => string.Equals(entry.PromptId, reply.ItemId, StringComparison.Ordinal));

        if (request.Kind == ProfileBuilderFeedbackKind.Approved &&
            latestOnReply?.Kind == ProfileBuilderFeedbackKind.Approved &&
            string.IsNullOrWhiteSpace(request.Comment))
        {
            return new ProfileBuilderFeedbackOutcome
            {
                Succeeded = true,
                Feedback = latestOnReply,
            };
        }

        if (request.Kind != ProfileBuilderFeedbackKind.Approved && string.IsNullOrWhiteSpace(request.Comment))
        {
            return ProfileBuilderFeedbackOutcome.Failed("Write what you would like the assistant to do differently.");
        }

        var replyIndex = IndexOf(conversation, reply);
        var customer = replyIndex > 0 && conversation[replyIndex - 1].Role == ChatRole.User
            ? conversation[replyIndex - 1]
            : null;

        var isLatestReply = conversation.Skip(replyIndex + 1).All(prompt => prompt.Role != ChatRole.Assistant);
        var regenerate = request.Kind == ProfileBuilderFeedbackKind.FeedbackAndRegenerate;

        if (regenerate && (!isLatestReply || customer is null))
        {
            return ProfileBuilderFeedbackOutcome.Failed("Only the latest reply to a customer message can be regenerated.");
        }

        CaptureManualChanges(session, interaction);

        var contextEnd = customer is null ? replyIndex : replyIndex - 1;
        var feedback = new ProfileBuilderFeedback
        {
            Id = IdGenerator.GenerateId(),
            Kind = request.Kind,
            PromptId = reply.ItemId,
            CustomerMessage = customer?.Text,
            AssistantReply = reply.Text,
            Context = ToTurns(conversation.Take(contextEnd)),
            Comment = request.Comment?.Trim(),
            WasRegenerated = regenerate,
            CreatedUtc = _clock.UtcNow,
        };

        ProfileBuilderRevision revision = null;

        if (request.Kind != ProfileBuilderFeedbackKind.Approved)
        {
            var current = ReadSettings(interaction, session);

            ProfileBuilderCoachResult result;

            try
            {
                result = await _coach.RewriteAsync(session, interaction, current, ToTurns(conversation), feedback, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The AI Profile Builder coach failed to rewrite the instructions of session '{SessionId}'.", session.ItemId);

                return ProfileBuilderFeedbackOutcome.Failed("The coach could not rewrite the instructions. Check the coach deployment and try again.");
            }

            if (result is null)
            {
                return ProfileBuilderFeedbackOutcome.Failed("No deployment is available to rewrite the instructions. Choose a chat deployment first.");
            }

            var updated = result.ApplyTo(current);

            revision = new ProfileBuilderRevision
            {
                Number = NextRevisionNumber(session),
                Source = ProfileBuilderRevisionSource.Feedback,
                FeedbackId = feedback.Id,
                Summary = string.IsNullOrWhiteSpace(result.Summary) ? "Updated the instructions from your feedback." : result.Summary.Trim(),
                Settings = updated,
                CreatedUtc = _clock.UtcNow,
            };

            feedback.RevisionNumber = revision.Number;
            session.Revisions.Add(revision);

            ApplySettings(updated, interaction, session);
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        session.Feedback.Add(feedback);

        if (regenerate)
        {
            // The rejected reply leaves the conversation so the next one is generated without it in the history;
            // the ledger keeps its text. The customer message goes too, because the client sends it again.
            await _promptStore.DeleteAsync(reply, cancellationToken);
            await _promptStore.DeleteAsync(customer, cancellationToken);
        }

        await SaveAsync(session, cancellationToken);

        return new ProfileBuilderFeedbackOutcome
        {
            Succeeded = true,
            Feedback = feedback,
            Revision = revision,
            RegenerateMessage = regenerate ? customer.Text : null,
        };
    }

    /// <summary>
    /// Makes an earlier revision current again, recording the restore as a new revision.
    /// </summary>
    /// <returns>The new revision, or <see langword="null"/> when the revision does not exist.</returns>
    public async Task<ProfileBuilderRevision> RestoreAsync(ProfileBuilderSession session, ChatInteraction interaction, int number, CancellationToken cancellationToken = default)
    {
        var source = session.Revisions.FirstOrDefault(revision => revision.Number == number);

        if (source is null)
        {
            return null;
        }

        CaptureManualChanges(session, interaction);

        var revision = new ProfileBuilderRevision
        {
            Number = NextRevisionNumber(session),
            Source = ProfileBuilderRevisionSource.Restore,
            Summary = $"Restored revision {number}.",
            Settings = source.Settings.Clone(),
            CreatedUtc = _clock.UtcNow,
        };

        session.Revisions.Add(revision);
        ApplySettings(revision.Settings, interaction, session);

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        await SaveAsync(session, cancellationToken);

        return revision;
    }

    /// <summary>
    /// Answers every situation the author gave feedback on again with the current settings, and has the judge say
    /// whether each answer still honours that feedback. Tools are not offered during a replay, so replaying a
    /// conversation never repeats an action such as sending a message.
    /// </summary>
    public async Task<ProfileBuilderReplay> ReplayAsync(ProfileBuilderSession session, ChatInteraction interaction, CancellationToken cancellationToken = default)
    {
        CaptureManualChanges(session, interaction);

        var current = ReadSettings(interaction, session);
        var replay = new ProfileBuilderReplay
        {
            RevisionNumber = session.Revisions.LastOrDefault()?.Number ?? 0,
            CreatedUtc = _clock.UtcNow,
        };

        var situations = GroupReplaySituations(session.Feedback);
        var deployment = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, interaction.ChatDeploymentName, cancellationToken: cancellationToken);
        var judged = new List<(string CaseId, string CustomerMessage, string Reply, IReadOnlyList<ProfileBuilderFeedback> Feedback)>();

        foreach (var situation in situations)
        {
            var anchor = situation[^1];
            var replayCase = new ProfileBuilderReplayCase
            {
                FeedbackId = anchor.Id,
                CustomerMessage = anchor.CustomerMessage,
            };

            replay.Cases.Add(replayCase);

            if (deployment is null)
            {
                replayCase.Reason = "No chat deployment is available to answer with.";

                continue;
            }

            try
            {
                replayCase.Reply = await AnswerAsync(deployment, interaction, current, anchor, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "The AI Profile Builder could not replay feedback '{FeedbackId}' of session '{SessionId}'.", anchor.Id, session.ItemId);
                replayCase.Reason = "The message could not be answered again.";

                continue;
            }

            judged.Add((anchor.Id, anchor.CustomerMessage, replayCase.Reply, situation));
        }

        if (judged.Count > 0)
        {
            try
            {
                var verdicts = await _coach.JudgeAsync(session, interaction, current, judged, cancellationToken);

                foreach (var replayCase in replay.Cases)
                {
                    if (verdicts.TryGetValue(replayCase.FeedbackId, out var verdict))
                    {
                        replayCase.Verdict = verdict.ToVerdict();
                        replayCase.Reason = verdict.Reason?.Trim();
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "The AI Profile Builder could not judge the replay of session '{SessionId}'.", session.ItemId);
            }
        }

        session.LastReplay = replay;
        await SaveAsync(session, cancellationToken);

        return replay;
    }

    /// <summary>
    /// Groups feedback into the situations a replay answers again: one per customer message in the same place in a
    /// conversation, so a reply rejected three times and fixed on the fourth is one situation judged against all
    /// four pieces of feedback. Feedback without a customer message, such as on an opening message, is skipped.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<ProfileBuilderFeedback>> GroupReplaySituations(IEnumerable<ProfileBuilderFeedback> feedback)
        => feedback
            .Where(entry => !string.IsNullOrWhiteSpace(entry.CustomerMessage))
            .GroupBy(entry => (Message: NormalizeText(entry.CustomerMessage), Depth: entry.Context?.Count ?? 0))
            .Select(group => (IReadOnlyList<ProfileBuilderFeedback>)group.OrderBy(entry => entry.CreatedUtc).ToArray())
            .OrderBy(group => group[0].CreatedUtc)
            .TakeLast(MaxReplayCases)
            .ToArray();

    /// <summary>
    /// Saves the session and stamps when it changed.
    /// </summary>
    public async Task SaveAsync(ProfileBuilderSession session, CancellationToken cancellationToken = default)
    {
        session.ModifiedUtc = _clock.UtcNow;

        await _sessionStore.UpdateAsync(session, cancellationToken);
    }

    /// <summary>
    /// Deletes a session together with its chat interaction and conversation.
    /// </summary>
    public async Task DeleteAsync(ProfileBuilderSession session, CancellationToken cancellationToken = default)
    {
        var interaction = await _interactionManager.FindByIdAsync(session.ChatInteractionId, cancellationToken);

        if (interaction is not null)
        {
            await _promptStore.DeleteAllPromptsAsync(interaction.ItemId);
            await _interactionManager.DeleteAsync(interaction, cancellationToken);
        }

        await _sessionStore.DeleteAsync(session, cancellationToken);
    }

    private async Task<string> AnswerAsync(AIDeployment deployment, ChatInteraction interaction, ProfileBuilderSettings settings, ProfileBuilderFeedback anchor, CancellationToken cancellationToken)
    {
        using var invocationScope = AIInvocationScope.Begin();
        using var usageScope = AIUsageScope.Begin(purpose: AIUsageFeaturePurposes.ProfileBuilderReplay);

        var context = await _completionContextBuilder.BuildAsync(interaction, completionContext =>
        {
            completionContext.SystemMessage = settings.SystemMessage;
            completionContext.DisableTools = true;
            completionContext.DataSourceId = null;
        }, cancellationToken);

        var messages = (anchor.Context ?? [])
            .Select(turn => new ChatMessage(IsAssistant(turn.Role) ? ChatRole.Assistant : ChatRole.User, turn.Text))
            .Append(new ChatMessage(ChatRole.User, anchor.CustomerMessage))
            .ToList();

        var response = await _completionService.CompleteAsync(deployment, messages, context, cancellationToken);

        return response?.Text;
    }

    private static List<ProfileBuilderTurn> ToTurns(IEnumerable<ChatInteractionPrompt> prompts)
        => prompts
            .Where(prompt => !string.IsNullOrWhiteSpace(prompt.Text))
            .Select(prompt => new ProfileBuilderTurn
            {
                Role = prompt.Role == ChatRole.Assistant ? "assistant" : "user",
                Text = prompt.Text,
            })
            .ToList();

    private static int IndexOf(IReadOnlyList<ChatInteractionPrompt> conversation, ChatInteractionPrompt prompt)
    {
        for (var index = 0; index < conversation.Count; index++)
        {
            if (ReferenceEquals(conversation[index], prompt))
            {
                return index;
            }
        }

        return -1;
    }

    private static int NextRevisionNumber(ProfileBuilderSession session)
        => session.Revisions.Count == 0 ? 1 : session.Revisions.Max(revision => revision.Number) + 1;

    private static bool IsAssistant(string role)
        => string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);

    internal static string NormalizeText(string text)
        => string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>
/// Feedback the author submits on a reply.
/// </summary>
public sealed class ProfileBuilderFeedbackRequest
{
    /// <summary>
    /// Gets or sets the identifier of the assistant prompt, when the client knows it.
    /// </summary>
    public string PromptId { get; set; }

    /// <summary>
    /// Gets or sets the text of the reply, used to find it when the identifier is not known.
    /// </summary>
    public string ReplyText { get; set; }

    /// <summary>
    /// Gets or sets the reply's position among the assistant replies shown, starting at zero.
    /// </summary>
    public int? AssistantIndex { get; set; }

    /// <summary>
    /// Gets or sets what the author asks for.
    /// </summary>
    public ProfileBuilderFeedbackKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the author's comment.
    /// </summary>
    public string Comment { get; set; }
}

/// <summary>
/// The result of submitting feedback.
/// </summary>
public sealed class ProfileBuilderFeedbackOutcome
{
    public bool Succeeded { get; set; }

    public string Error { get; set; }

    public ProfileBuilderFeedback Feedback { get; set; }

    public ProfileBuilderRevision Revision { get; set; }

    /// <summary>
    /// Gets or sets the customer message the client sends again to regenerate the reply, when one was asked for.
    /// </summary>
    public string RegenerateMessage { get; set; }

    public static ProfileBuilderFeedbackOutcome Failed(string error)
        => new() { Succeeded = false, Error = error };
}
