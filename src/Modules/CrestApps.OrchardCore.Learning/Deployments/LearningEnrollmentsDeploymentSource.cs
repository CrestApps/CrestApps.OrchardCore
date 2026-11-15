using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Recipes;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Learning.Deployments;

/// <summary>
/// Writes the enrollments step, 500 enrollments a page, with people named by user name. It writes nothing for a person
/// without the Export learner records permission, and tells the export observers what it wrote.
/// </summary>
internal sealed class LearningEnrollmentsDeploymentSource : DeploymentSourceBase<LearningEnrollmentsDeploymentStep>
{
    private const int PageSize = 500;

    private readonly EnrollmentStore _enrollmentStore;
    private readonly EnrollmentProgressStore _progressStore;
    private readonly LearnerUserLookup _userLookup;
    private readonly CourseEnrollmentKeyStore _keyStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ILearnerRecordExportObserver> _exportObservers;
    private readonly ILogger _logger;

    public LearningEnrollmentsDeploymentSource(
        EnrollmentStore enrollmentStore,
        EnrollmentProgressStore progressStore,
        LearnerUserLookup userLookup,
        CourseEnrollmentKeyStore keyStore,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<ILearnerRecordExportObserver> exportObservers,
        ILogger<LearningEnrollmentsDeploymentSource> logger)
    {
        _enrollmentStore = enrollmentStore;
        _progressStore = progressStore;
        _userLookup = userLookup;
        _keyStore = keyStore;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _exportObservers = exportObservers;
        _logger = logger;
    }

    protected override async Task ProcessAsync(LearningEnrollmentsDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // A deployment plan can also run without a request, such as a scheduled remote deployment; then the plan's own
        // permission check has already passed.
        if (user?.Identity?.IsAuthenticated == true && !await _authorizationService.AuthorizeAsync(user, LearningPermissions.ExportLearnerRecords))
        {
            _logger.LogWarning("Learner records were not exported: the user lacks the Export learner records permission.");

            return;
        }

        var rows = new JsonArray();
        var skip = 0;

        while (true)
        {
            var page = await _enrollmentStore.PageForExportAsync(step.CourseId, step.FromUtc, step.ToUtc, skip, PageSize);

            if (page.Count == 0)
            {
                break;
            }

            var userNames = await _userLookup.GetUserNamesAsync(page.Select(enrollment => enrollment.UserId).Concat(page.SelectMany(enrollment => enrollment.History.Select(change => change.Actor))));
            var progress = step.IncludeProgress
                ? (await _progressStore.ListByEnrollmentsAsync(page.Select(enrollment => enrollment.ItemId).ToArray())).ToDictionary(item => item.EnrollmentId, StringComparer.Ordinal)
                : null;

            foreach (var enrollment in page)
            {
                var node = JsonSerializer.SerializeToNode(enrollment, LearningEnrollmentsRecipeStep.JsonOptions).AsObject();

                node.Remove("UserId");
                node.Remove("userId");
                node["UserName"] = userNames.TryGetValue(enrollment.UserId ?? string.Empty, out var userName) ? userName : null;

                if (node["History"] is JsonArray history)
                {
                    foreach (var change in history.OfType<JsonObject>())
                    {
                        var actor = change["Actor"]?.GetValue<string>();

                        if (actor is not null && userNames.TryGetValue(actor, out var actorName))
                        {
                            change["ActorUserName"] = actorName;
                        }
                    }
                }

                if (progress is not null && progress.TryGetValue(enrollment.ItemId, out var itemProgress))
                {
                    node["Progress"] = JsonSerializer.SerializeToNode(itemProgress, LearningEnrollmentsRecipeStep.JsonOptions);
                }

                rows.Add(node);
            }

            skip += page.Count;

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var keys = new JsonArray();

        foreach (var key in await _keyStore.ListAllAsync())
        {
            if (step.CourseId is null || key.CourseId == step.CourseId)
            {
                keys.Add(new JsonObject { ["CourseId"] = key.CourseId, ["Hash"] = key.Hash });
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["CourseKeys"] = keys,
            ["Enrollments"] = rows,
        });

        var filter = string.Join(
            "; ",
            new[]
            {
                step.CourseId is null ? null : "Course " + step.CourseId,
                step.FromUtc is null ? null : "From " + step.FromUtc.Value.ToString("O", CultureInfo.InvariantCulture),
                step.ToUtc is null ? null : "To " + step.ToUtc.Value.ToString("O", CultureInfo.InvariantCulture),
            }.Where(part => part is not null));

        foreach (var observer in _exportObservers)
        {
            await observer.ExportedAsync(step.Name, rows.Count, user?.FindFirstValue(ClaimTypes.NameIdentifier), filter);
        }
    }
}
