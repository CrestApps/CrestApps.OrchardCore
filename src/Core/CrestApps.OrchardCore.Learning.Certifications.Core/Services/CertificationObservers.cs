using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// Checks the certification paths of a completed course. A failure is logged; the upkeep catches up on it.
/// </summary>
public sealed class CertificationEnrollmentObserver : IEnrollmentObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationEnrollmentObserver"/> class.
    /// </summary>
    /// <param name="services">The services; the path service is resolved when needed, because it enrolls people and so
    /// depends on the enrollment observers itself.</param>
    /// <param name="logger">The logger.</param>
    public CertificationEnrollmentObserver(IServiceProvider services, ILogger<CertificationEnrollmentObserver> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => CertificationTriggers.EvaluateAsync(_services.GetRequiredService<ICertificationPathService>(), enrollment.CourseId, enrollment.UserId, _logger, cancellationToken);

    /// <inheritdoc/>
    public Task ReviewClearedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => enrollment?.Status == EnrollmentStatus.Completed ? CompletedAsync(enrollment, cancellationToken) : Task.CompletedTask;
}

/// <summary>
/// Checks the certification paths of a passed quiz. A failure is logged; the upkeep catches up on it.
/// </summary>
public sealed class CertificationAttemptObserver : IAttemptObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationAttemptObserver"/> class.
    /// </summary>
    /// <param name="services">The services; the path service is resolved when needed.</param>
    /// <param name="logger">The logger.</param>
    public CertificationAttemptObserver(IServiceProvider services, ILogger<CertificationAttemptObserver> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task SubmittedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => PassedAsync(attempt, cancellationToken);

    /// <inheritdoc/>
    public Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => PassedAsync(attempt, cancellationToken);

    /// <inheritdoc/>
    public Task ScoreChangedAsync(AssessmentAttempt attempt, AttemptScoreChange change, CancellationToken cancellationToken = default)
        => PassedAsync(attempt, cancellationToken);

    private Task PassedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken)
        => attempt.Passed == true
            ? CertificationTriggers.EvaluateAsync(_services.GetRequiredService<ICertificationPathService>(), attempt.QuizId, attempt.UserId, _logger, cancellationToken)
            : Task.CompletedTask;
}

/// <summary>
/// Moves certification path progress when its Skills certificate expires or is revoked.
/// </summary>
public sealed class CertificationSkillsObserver : ICertificationExpiryObserver, ICertificationRecordObserver
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationSkillsObserver"/> class.
    /// </summary>
    /// <param name="services">The services; the path service is resolved when needed, because it issues certificates
    /// and so depends on the Skills observers itself.</param>
    public CertificationSkillsObserver(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc/>
    public Task ExpiringAsync(CertificationRecord record, Certification certification, int daysBefore, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ExpiredAsync(CertificationRecord record, Certification certification, CancellationToken cancellationToken = default)
        => record.Source == CertificationsConstants.Source ? _services.GetRequiredService<DefaultCertificationPathService>().LapseAsync(record.ItemId, cancellationToken) : Task.CompletedTask;

    /// <inheritdoc/>
    public Task RevokedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
        => context.Record.Source == CertificationsConstants.Source ? _services.GetRequiredService<DefaultCertificationPathService>().RevokeAsync(context.Record.ItemId, cancellationToken) : Task.CompletedTask;
}

internal static class CertificationTriggers
{
    public static async Task EvaluateAsync(ICertificationPathService paths, string targetId, string userId, ILogger logger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(userId))
        {
            return;
        }

        try
        {
            foreach (var path in await paths.ListByTargetAsync(targetId, cancellationToken))
            {
                await paths.EvaluateAsync(path.ItemId, userId, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Checking the certification paths of '{TargetId}' for the user '{UserId}' failed; the certification upkeep checks again.", targetId, userId);
        }
    }
}
