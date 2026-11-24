using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Starts the people of a linked cohort on a learning path, and takes off a person who is in no linked cohort any more,
/// but only from a path the cohort sync started: a path the person or required learning started stays, and so does a
/// completed one.
/// </summary>
internal sealed class LearningPathCohortLearningTarget : ICohortLearningTarget
{
    private readonly ILearningPathService _paths;
    private readonly ILogger _logger;

    public LearningPathCohortLearningTarget(ILearningPathService paths, ILogger<LearningPathCohortLearningTarget> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public string PartName => nameof(LearningPathPart);

    public async Task JoinAsync(string targetId, string cohortId, string userId, string actor, CancellationToken cancellationToken = default)
    {
        var result = await _paths.StartAsync(new LearningPathStartRequest
        {
            PathId = targetId,
            UserId = userId,
            Source = LearningConstants.EnrollmentSources.Cohort,
            SourceId = cohortId,
            Actor = actor,
        }, cancellationToken);

        if (result.Progress is null)
        {
            _logger.LogWarning("The cohort sync could not start a person on a learning path: {Error}", result.Error);
        }
    }

    public async Task LeaveAsync(string targetId, string cohortId, string userId, bool inAnotherLinkedCohort, string actor, CancellationToken cancellationToken = default)
    {
        if (inAnotherLinkedCohort)
        {
            return;
        }

        await _paths.StopAsync(targetId, userId, LearningConstants.EnrollmentSources.Cohort, actor, "No longer in a cohort linked to the learning path", cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> ListJoinedUserIdsAsync(string targetId, string cohortId, CancellationToken cancellationToken = default)
    {
        const int PageSize = 200;
        var joined = new HashSet<string>(StringComparer.Ordinal);

        for (var skip = 0; ; skip += PageSize)
        {
            var (count, items) = await _paths.PageAsync(targetId, null, skip, PageSize, cancellationToken);

            joined.UnionWith(items
                .Where(progress => progress.Source == LearningConstants.EnrollmentSources.Cohort && string.Equals(progress.SourceId, cohortId, StringComparison.Ordinal))
                .Select(progress => progress.UserId));

            if (items.Count == 0 || skip + PageSize >= count)
            {
                return joined;
            }
        }
    }
}

/// <summary>
/// Lets cohorts be linked to learning paths when enrollment by cohort is on.
/// </summary>
[RequireFeatures(LearningConstants.Features.Cohorts)]
public sealed class CohortsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCohortLearningTarget<LearningPathCohortLearningTarget>();
    }
}
