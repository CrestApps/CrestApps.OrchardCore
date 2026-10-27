using System.Globalization;
using System.Text;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Tests a builder session's instructions without a person: a model plays a customer in each of the session's
/// scenarios, the instructions answer as they would in a real conversation, tools included, and another model judges
/// each conversation. When a conversation fails, the coach rewrites the instructions from the judge's findings, the
/// same way it does from an author's feedback, and the next round tests the new revision.
/// </summary>
public sealed class ProfileBuilderSimulator
{
    /// <summary>
    /// The most simulated conversations a session keeps.
    /// </summary>
    internal const int MaxKeptSimulations = 60;

    private readonly AIConversationSimulator _simulator;
    private readonly IAIConversationEvaluator _evaluator;
    private readonly ProfileBuilderCoach _coach;
    private readonly ICatalogManager<ChatInteraction> _interactionManager;
    private readonly IAIProfileManager _profileManager;
    private readonly ProfileBuilderService _builderService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileBuilderSimulator"/> class.
    /// </summary>
    public ProfileBuilderSimulator(
        AIConversationSimulator simulator,
        IAIConversationEvaluator evaluator,
        ProfileBuilderCoach coach,
        ICatalogManager<ChatInteraction> interactionManager,
        IAIProfileManager profileManager,
        ProfileBuilderService builderService,
        IClock clock,
        ILogger<ProfileBuilderSimulator> logger)
    {
        _simulator = simulator;
        _evaluator = evaluator;
        _coach = coach;
        _interactionManager = interactionManager;
        _profileManager = profileManager;
        _builderService = builderService;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Runs one round: every scenario once against the current instructions. When a scenario fails and the settings
    /// ask for it, the coach then rewrites the instructions, so the next round tests the rewrite.
    /// </summary>
    /// <param name="session">The builder session.</param>
    /// <param name="interaction">The session's chat interaction.</param>
    /// <param name="runId">The run the round belongs to, or <see langword="null"/> to start a new run.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What the round did.</returns>
    public async Task<ProfileBuilderSimulationRound> RunRoundAsync(ProfileBuilderSession session, ChatInteraction interaction, string runId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(interaction);

        var settings = Normalize(session.SimulationSettings);

        if (settings.Scenarios.Count == 0)
        {
            return ProfileBuilderSimulationRound.Failed("Describe at least one customer to play.");
        }

        _builderService.CaptureManualChanges(session, interaction);

        var round = string.IsNullOrEmpty(runId)
            ? 1
            : session.Simulations.Where(simulation => simulation.RunId == runId).Select(simulation => simulation.Round).DefaultIfEmpty(0).Max() + 1;

        runId = string.IsNullOrEmpty(runId) ? IdGenerator.GenerateId() : runId;

        if (round > settings.Rounds)
        {
            return ProfileBuilderSimulationRound.Failed("This run already took every round it was allowed.");
        }

        var revisionNumber = session.Revisions.Count == 0 ? 1 : session.Revisions.Max(revision => revision.Number);
        var profile = await BuildProfileUnderTestAsync(session, interaction);
        var results = new List<ProfileBuilderSimulation>();

        foreach (var scenario in settings.Scenarios)
        {
            results.Add(await RunScenarioAsync(profile, settings, scenario, runId, round, revisionNumber, cancellationToken));
        }

        session.Simulations.AddRange(results);

        if (session.Simulations.Count > MaxKeptSimulations)
        {
            session.Simulations.RemoveRange(0, session.Simulations.Count - MaxKeptSimulations);
        }

        var outcome = new ProfileBuilderSimulationRound
        {
            Succeeded = true,
            RunId = runId,
            Round = round,
            Simulations = results,
            AllPassed = results.All(result => result.Passed),
        };

        var failed = results.Where(result => !result.Passed && result.Error is null).ToList();

        if (failed.Count > 0 && settings.Improve && round < settings.Rounds)
        {
            outcome.Revision = await ImproveAsync(session, interaction, failed, round, cancellationToken);
        }

        outcome.Finished = outcome.AllPassed || round >= settings.Rounds || (failed.Count > 0 && outcome.Revision is null);

        await _builderService.SaveAsync(session, cancellationToken);

        return outcome;
    }

    /// <summary>
    /// Keeps the settings within their limits, and drops empty scenarios and criteria.
    /// </summary>
    /// <param name="settings">The settings.</param>
    internal static ProfileBuilderSimulationSettings Normalize(ProfileBuilderSimulationSettings settings)
    {
        settings ??= new ProfileBuilderSimulationSettings();

        settings.Scenarios = (settings.Scenarios ?? [])
            .Where(scenario => !string.IsNullOrWhiteSpace(scenario))
            .Select(scenario => scenario.Trim())
            .Take(ProfileBuilderSimulationSettings.MaxScenarios)
            .ToList();

        settings.Criteria = (settings.Criteria ?? [])
            .Where(criterion => !string.IsNullOrWhiteSpace(criterion))
            .Select(criterion => criterion.Trim())
            .ToList();

        settings.CustomerDeploymentName = string.IsNullOrWhiteSpace(settings.CustomerDeploymentName) ? null : settings.CustomerDeploymentName.Trim();
        settings.JudgeDeploymentName = string.IsNullOrWhiteSpace(settings.JudgeDeploymentName) ? null : settings.JudgeDeploymentName.Trim();
        settings.MaxTurns = Math.Clamp(settings.MaxTurns, 1, 20);
        settings.Rounds = Math.Clamp(settings.Rounds, 1, ProfileBuilderSimulationSettings.MaxRounds);
        settings.PassingScore = Math.Clamp(settings.PassingScore, 0, 100);

        return settings;
    }

    /// <summary>
    /// Writes what the judge found in the failed conversations as feedback the coach can act on: its summaries, the
    /// problems with the lines that show them, and the criteria that were not met.
    /// </summary>
    /// <param name="failed">The conversations that failed.</param>
    /// <param name="passingScore">The score a conversation needs.</param>
    internal static string BuildFeedbackComment(IReadOnlyList<ProfileBuilderSimulation> failed, int passingScore)
    {
        var builder = new StringBuilder();
        builder.Append("Simulated customers tested these instructions and ")
            .Append(failed.Count == 1 ? "one conversation" : failed.Count.ToString(CultureInfo.InvariantCulture) + " conversations")
            .Append(" failed (a conversation passes at ")
            .Append(passingScore.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" out of 100 with every criterion met). Fix what the judge found without making the assistant worse at what already worked.");

        foreach (var simulation in failed)
        {
            builder.AppendLine();
            builder.Append("Customer: ").AppendLine(OneLine(simulation.Scenario));
            builder.Append("Score: ").Append(simulation.Score.ToString(CultureInfo.InvariantCulture)).AppendLine("/100");

            if (!string.IsNullOrWhiteSpace(simulation.Summary))
            {
                builder.Append("Judge: ").AppendLine(OneLine(simulation.Summary));
            }

            foreach (var problem in simulation.Problems)
            {
                builder.Append("- ").AppendLine(OneLine(problem));
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Picks the reply the feedback is about: the assistant line a problem quotes, else the assistant's last line,
    /// with the customer line before it.
    /// </summary>
    /// <param name="simulation">The failed conversation.</param>
    internal static (string CustomerMessage, string Reply, int Index) FindReply(ProfileBuilderSimulation simulation)
    {
        var transcript = simulation.Transcript;
        var index = -1;

        foreach (var problem in simulation.Problems)
        {
            var quoteStart = problem.IndexOf('"');
            var quoteEnd = problem.LastIndexOf('"');

            if (quoteStart < 0 || quoteEnd <= quoteStart + 1)
            {
                continue;
            }

            var quote = problem[(quoteStart + 1)..quoteEnd].Trim();
            index = transcript.FindIndex(turn => IsAssistant(turn) && turn.Text?.Contains(quote, StringComparison.OrdinalIgnoreCase) == true);

            if (index >= 0)
            {
                break;
            }
        }

        if (index < 0)
        {
            index = transcript.FindLastIndex(IsAssistant);
        }

        if (index < 0)
        {
            return (null, null, -1);
        }

        var customer = index > 0 && !IsAssistant(transcript[index - 1]) ? transcript[index - 1].Text : null;

        return (customer, transcript[index].Text, index);
    }

    private async Task<ProfileBuilderSimulation> RunScenarioAsync(
        AIProfile profile,
        ProfileBuilderSimulationSettings settings,
        string scenario,
        string runId,
        int round,
        int revisionNumber,
        CancellationToken cancellationToken)
    {
        var simulation = new ProfileBuilderSimulation
        {
            RunId = runId,
            Round = round,
            RevisionNumber = revisionNumber,
            Scenario = scenario,
            CreatedUtc = _clock.UtcNow,
        };

        try
        {
            var conversation = await _simulator.SimulateAsync(new AIConversationSimulationRequest
            {
                Profile = profile,
                Scenario = scenario,
                MaxTurns = settings.MaxTurns,
                CustomerDeploymentName = settings.CustomerDeploymentName,
            }, cancellationToken);

            simulation.Transcript = conversation.Turns
                .Select(turn => new ProfileBuilderTurn
                {
                    // The builder's own conversations use the chat roles.
                    Role = turn.Role == AIConversationTurn.CustomerRole ? "user" : "assistant",
                    Text = turn.Text,
                })
                .ToList();

            var evaluation = await _evaluator.EvaluateAsync(new AIConversationEvaluationRequest
            {
                Turns = conversation.Turns,
                Criteria = settings.Criteria,
                PassingScore = settings.PassingScore,
                Instructions = conversation.Instructions,
                DeploymentName = settings.JudgeDeploymentName,
            }, cancellationToken);

            if (evaluation is null)
            {
                simulation.Error = "The conversation could not be judged: the customer said nothing, or no deployment can judge.";

                return simulation;
            }

            simulation.Score = evaluation.Score;
            simulation.Passed = evaluation.Passed;
            simulation.Summary = evaluation.Summary;
            simulation.Problems = evaluation.Issues
                .Select(issue => string.IsNullOrWhiteSpace(issue.Quote)
                    ? $"{issue.Kind}: {issue.Detail}"
                    : $"{issue.Kind}: {issue.Detail} (\"{issue.Quote}\")")
                .Concat(evaluation.Criteria
                    .Where(criterion => !criterion.Met)
                    .Select(criterion => $"Criterion not met: {criterion.Name.TrimEnd('.')}. {criterion.Reason}"))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "A simulated conversation of an AI Profile Builder session failed.");
            simulation.Error = ex is InvalidOperationException ? ex.Message : "The simulated conversation failed. Check the deployments and try again.";
        }

        return simulation;
    }

    private async Task<ProfileBuilderRevision> ImproveAsync(ProfileBuilderSession session, ChatInteraction interaction, List<ProfileBuilderSimulation> failed, int round, CancellationToken cancellationToken)
    {
        var worst = failed.OrderBy(simulation => simulation.Score).First();
        var (customerMessage, reply, index) = FindReply(worst);

        var feedback = new ProfileBuilderFeedback
        {
            Id = IdGenerator.GenerateId(),
            Kind = ProfileBuilderFeedbackKind.Feedback,
            CustomerMessage = customerMessage,
            AssistantReply = reply,
            Context = index > 1 ? worst.Transcript.Take(index - 1).ToList() : [],
            Comment = BuildFeedbackComment(failed, Normalize(session.SimulationSettings).PassingScore),
            CreatedUtc = _clock.UtcNow,
        };

        var current = ProfileBuilderService.ReadSettings(interaction, session);
        ProfileBuilderCoachResult result;

        try
        {
            result = await _coach.RewriteAsync(session, interaction, current, worst.Transcript, feedback, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The AI Profile Builder coach failed to rewrite the instructions after a simulation.");

            return null;
        }

        if (result is null)
        {
            return null;
        }

        var revision = new ProfileBuilderRevision
        {
            Number = session.Revisions.Count == 0 ? 1 : session.Revisions.Max(item => item.Number) + 1,
            Source = ProfileBuilderRevisionSource.Simulation,
            FeedbackId = feedback.Id,
            Summary = string.IsNullOrWhiteSpace(result.Summary)
                ? $"Updated the instructions after round {round} of the simulation."
                : result.Summary.Trim(),
            Settings = result.ApplyTo(current),
            CreatedUtc = _clock.UtcNow,
        };

        feedback.RevisionNumber = revision.Number;
        session.Feedback.Add(feedback);
        session.Revisions.Add(revision);

        ProfileBuilderService.ApplySettings(revision.Settings, interaction, session);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        return revision;
    }

    // The profile the simulated customer talks to: the linked profile, so its knowledge, tools and settings take
    // part, with the session's instructions and capabilities on top; or a profile made from the session alone.
    private async Task<AIProfile> BuildProfileUnderTestAsync(ProfileBuilderSession session, ChatInteraction interaction)
    {
        var linked = string.IsNullOrEmpty(session.ProfileId) ? null : await _profileManager.FindByIdAsync(session.ProfileId);
        var profile = linked?.Clone() ?? new AIProfile
        {
            ItemId = session.ItemId,
            Name = "profile-builder-" + session.ItemId,
            DisplayText = interaction.Title,
            Type = AIProfileType.Chat,
        };

        ProfileBuilderProfileMapper.CopyToProfile(interaction, session, profile);

        return profile;
    }

    private static bool IsAssistant(ProfileBuilderTurn turn)
        => string.Equals(turn.Role, AIConversationTurn.AssistantRole, StringComparison.OrdinalIgnoreCase);

    private static string OneLine(string text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : text.ReplaceLineEndings(" ").Trim();
}

/// <summary>
/// What one round of a simulation run did.
/// </summary>
public sealed class ProfileBuilderSimulationRound
{
    /// <summary>
    /// Gets or sets whether the round ran.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets why the round could not run.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the run, which the next round continues.
    /// </summary>
    public string RunId { get; set; }

    /// <summary>
    /// Gets or sets the round, starting at 1.
    /// </summary>
    public int Round { get; set; }

    /// <summary>
    /// Gets or sets the conversations of the round.
    /// </summary>
    public IReadOnlyList<ProfileBuilderSimulation> Simulations { get; set; } = [];

    /// <summary>
    /// Gets or sets whether every conversation passed.
    /// </summary>
    public bool AllPassed { get; set; }

    /// <summary>
    /// Gets or sets the revision the coach wrote after the round, or <see langword="null"/> when it wrote none.
    /// </summary>
    public ProfileBuilderRevision Revision { get; set; }

    /// <summary>
    /// Gets or sets whether the run is over: everything passed, the rounds ran out, or the coach could not help.
    /// </summary>
    public bool Finished { get; set; }

    /// <summary>
    /// Creates a round that could not run.
    /// </summary>
    /// <param name="error">Why.</param>
    public static ProfileBuilderSimulationRound Failed(string error)
        => new()
        {
            Error = error,
            Finished = true,
        };
}
