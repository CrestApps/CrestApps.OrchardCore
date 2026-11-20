using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// Grants the competencies of a course (its <see cref="CompetenciesPart"/>) when an enrollment completes it, only when
/// the completion supports what is issued on it (<see cref="CompletionEvidence.SupportsIssuing"/>), the rule
/// credentials and education credits follow: a failed completion, one under review or one resting on a result the
/// browser reported grants nothing. Granting again changes nothing, so a completion delivered twice is harmless.
/// </summary>
public sealed class CompetencyEnrollmentObserver : IEnrollmentObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public CompetencyEnrollmentObserver(IServiceProvider services, ILogger<CompetencyEnrollmentObserver> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        if (!CompletionEvidence.SupportsIssuing(enrollment))
        {
            return;
        }

        try
        {
            var course = await _services.GetRequiredService<IContentManager>().GetAsync(enrollment.CourseId, VersionOptions.Published);

            if (course is null || !course.TryGet<CompetenciesPart>(out var part) || part.SkillIds.Count == 0)
            {
                return;
            }

            var earnedOn = await CompetencyDates.EarnedOnAsync(_services, enrollment.CompletedUtc);

            await CompetencyGrants.GrantAsync(_services.GetRequiredService<ISkillAssignmentService>(), part, enrollment.UserId, earnedOn, CompetencyGrants.Actor(course.DisplayText), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The competencies of a completed course could not be granted.");
        }
    }
}

/// <summary>
/// Turns the UTC time of a completion into the day it happened in the site's time zone.
/// </summary>
public static class CompetencyDates
{
    /// <summary>
    /// Gets the day of a completion in the site's time zone, today when the time is unknown.
    /// </summary>
    /// <param name="services">The services, for the clock and the local clock.</param>
    /// <param name="completedUtc">The UTC time of the completion.</param>
    /// <returns>The day.</returns>
    public static async Task<DateOnly> EarnedOnAsync(IServiceProvider services, DateTime? completedUtc)
    {
        ArgumentNullException.ThrowIfNull(services);

        var utc = completedUtc ?? services.GetRequiredService<IClock>().UtcNow;
        var local = await services.GetRequiredService<ILocalClock>().ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));

        return DateOnly.FromDateTime(local.DateTime);
    }
}
