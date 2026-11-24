using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Recipes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Learning.Assessments.Deployments;

/// <summary>
/// Writes the attempts step, 500 attempts a page, with people named by user name. It writes nothing for a person without
/// the Export learner records permission, and tells the export observers what it wrote.
/// </summary>
internal sealed class LearningAttemptsDeploymentSource : DeploymentSourceBase<LearningAttemptsDeploymentStep>
{
    private const int PageSize = 500;

    private readonly AssessmentAttemptStore _attemptStore;
    private readonly AssessmentOverrideStore _overrideStore;
    private readonly LearningSecretStore _secretStore;
    private readonly LearnerUserLookup _userLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ILearnerRecordExportObserver> _exportObservers;
    private readonly ILogger _logger;

    public LearningAttemptsDeploymentSource(
        AssessmentAttemptStore attemptStore,
        AssessmentOverrideStore overrideStore,
        LearningSecretStore secretStore,
        LearnerUserLookup userLookup,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<ILearnerRecordExportObserver> exportObservers,
        ILogger<LearningAttemptsDeploymentSource> logger)
    {
        _attemptStore = attemptStore;
        _overrideStore = overrideStore;
        _secretStore = secretStore;
        _userLookup = userLookup;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _exportObservers = exportObservers;
        _logger = logger;
    }

    protected override async Task ProcessAsync(LearningAttemptsDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // A deployment plan can also run without a request, such as a scheduled remote deployment; then the plan's own
        // permission check has already passed.
        if (user?.Identity?.IsAuthenticated == true && !await _authorizationService.AuthorizeAsync(user, LearningPermissions.ExportLearnerRecords))
        {
            _logger.LogWarning("Quiz attempts were not exported: the user lacks the Export learner records permission.");

            return;
        }

        var attempts = new JsonArray();
        var skip = 0;

        while (true)
        {
            var page = await _attemptStore.PageForExportAsync(step.QuizId, null, null, skip, PageSize);

            if (page.Count == 0)
            {
                break;
            }

            var names = await _userLookup.GetUserNamesAsync(page.Select(attempt => attempt.UserId)
                .Concat(page.Select(attempt => attempt.GradedBy))
                .Concat(page.SelectMany(attempt => attempt.ScoreHistory.Select(change => change.Actor))));

            foreach (var attempt in page)
            {
                var node = JsonSerializer.SerializeToNode(attempt, LearningAttemptsRecipeStep.JsonOptions).AsObject();

                node.Remove("UserId");
                node.Remove("userId");
                node["UserName"] = names.TryGetValue(attempt.UserId ?? string.Empty, out var userName) ? userName : null;

                if (attempt.GradedBy is not null && names.TryGetValue(attempt.GradedBy, out var grader))
                {
                    node["GradedByUserName"] = grader;
                }

                if (node["ScoreHistory"] is JsonArray history)
                {
                    foreach (var change in history.OfType<JsonObject>())
                    {
                        var actor = change["Actor"]?.GetValue<string>();

                        if (actor is not null && names.TryGetValue(actor, out var actorName))
                        {
                            change["ActorUserName"] = actorName;
                        }
                    }
                }

                attempts.Add(node);
            }

            skip += page.Count;

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var overrides = new JsonArray();

        if (step.IncludeOverrides)
        {
            var entries = string.IsNullOrEmpty(step.QuizId) ? await _overrideStore.GetAllAsync() : await _overrideStore.ListByQuizAsync(step.QuizId);
            var names = await _userLookup.GetUserNamesAsync(entries.Select(entry => entry.UserId).Concat(entries.Select(entry => entry.Actor)));

            foreach (var entry in entries)
            {
                var node = JsonSerializer.SerializeToNode(entry, LearningAttemptsRecipeStep.JsonOptions).AsObject();

                node.Remove("UserId");
                node.Remove("userId");
                node["UserName"] = names.TryGetValue(entry.UserId ?? string.Empty, out var userName) ? userName : null;

                if (entry.Actor is not null && names.TryGetValue(entry.Actor, out var actorName))
                {
                    node["ActorUserName"] = actorName;
                }

                overrides.Add(node);
            }
        }

        var codes = new JsonArray();

        foreach (var code in await _secretStore.ListAsync(AssessmentsConstants.QuizAccessCodeSecret))
        {
            if (string.IsNullOrEmpty(step.QuizId) || code.OwnerId == step.QuizId)
            {
                codes.Add(new JsonObject { ["QuizId"] = code.OwnerId, ["Hash"] = code.Hash });
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["AccessCodes"] = codes,
            ["Attempts"] = attempts,
            ["Overrides"] = overrides,
        });

        foreach (var observer in _exportObservers)
        {
            await observer.ExportedAsync(step.Name, attempts.Count, user?.FindFirstValue(ClaimTypes.NameIdentifier), step.QuizId is null ? null : "Quiz " + step.QuizId);
        }
    }
}
