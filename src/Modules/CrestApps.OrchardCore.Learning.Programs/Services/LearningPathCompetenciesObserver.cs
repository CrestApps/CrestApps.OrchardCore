using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Grants the competencies of a learning path (its <see cref="CompetenciesPart"/>) when a person completes it, only
/// when the completed path may support what is issued on it (<see cref="ILearningPathService.SupportsIssuingAsync"/>),
/// as the path's credential and credits are. A Moodle learning-plan template becomes such a path.
/// </summary>
internal sealed class LearningPathCompetenciesObserver : ILearningPathObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public LearningPathCompetenciesObserver(IServiceProvider services, ILogger<LearningPathCompetenciesObserver> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task CompletedAsync(LearningPathProgress progress, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = await _services.GetRequiredService<IContentManager>().GetAsync(progress.PathId, VersionOptions.Published);

            if (path is null || !path.TryGet<CompetenciesPart>(out var part) || part.SkillIds.Count == 0)
            {
                return;
            }

            if (!await _services.GetRequiredService<ILearningPathService>().SupportsIssuingAsync(progress, cancellationToken))
            {
                return;
            }

            var earnedOn = await CompetencyDates.EarnedOnAsync(_services, progress.CompletedUtc);

            await CompetencyGrants.GrantAsync(_services.GetRequiredService<ISkillAssignmentService>(), part, progress.UserId, earnedOn, CompetencyGrants.Actor(path.DisplayText), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The competencies of a completed learning path could not be granted.");
        }
    }
}
