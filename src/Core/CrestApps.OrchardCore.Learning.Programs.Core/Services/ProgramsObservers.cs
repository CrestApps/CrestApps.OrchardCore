using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.Programs.Core.Services;

/// <summary>
/// Moves people through their learning paths and required learning when they complete a course. The services are
/// resolved when an event arrives, because they enroll people themselves.
/// </summary>
internal sealed class ProgramsEnrollmentObserver : IEnrollmentObserver
{
    private readonly IServiceProvider _services;

    public ProgramsEnrollmentObserver(IServiceProvider services)
    {
        _services = services;
    }

    public async Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        await _services.GetRequiredService<ILearningPathService>().EvaluateCourseAsync(enrollment.UserId, enrollment.CourseId, cancellationToken);
        await RefreshRequiredAsync(enrollment, cancellationToken);
    }

    public Task CourseStartedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => RefreshRequiredAsync(enrollment, cancellationToken);

    private async Task RefreshRequiredAsync(Enrollment enrollment, CancellationToken cancellationToken)
    {
        if (_services.GetService<IRequiredLearningService>() is { } required)
        {
            await required.RefreshTargetAsync(ProgramsConstants.TargetKinds.Course, enrollment.CourseId, enrollment.UserId, cancellationToken);
        }
    }
}

/// <summary>
/// Completes required learning about a learning path when the path is completed.
/// </summary>
/// <summary>
/// Refreshes the required learning of a certification path as soon as a certificate is issued, so nobody who certified
/// after the day's run is declared overdue.
/// </summary>
internal sealed class RequiredLearningCertificationObserver : CrestApps.OrchardCore.Learning.Certifications.Services.ICertificationPathObserver
{
    private readonly IServiceProvider _services;

    public RequiredLearningCertificationObserver(IServiceProvider services)
    {
        _services = services;
    }

    public Task CertifiedAsync(CrestApps.OrchardCore.Learning.Certifications.Models.CertificationPathProgress progress, CrestApps.OrchardCore.Learning.Certifications.Models.CertificationPath path, CancellationToken cancellationToken = default)
        => _services.GetService<IRequiredLearningService>() is { } required
            ? required.RefreshTargetAsync(ProgramsConstants.TargetKinds.CertificationPath, progress.PathId, progress.UserId, cancellationToken)
            : Task.CompletedTask;
}

internal sealed class RequiredLearningPathObserver : ILearningPathObserver
{
    private readonly IServiceProvider _services;

    public RequiredLearningPathObserver(IServiceProvider services)
    {
        _services = services;
    }

    public async Task CompletedAsync(LearningPathProgress progress, CancellationToken cancellationToken = default)
    {
        if (_services.GetService<IRequiredLearningService>() is { } required)
        {
            await required.RefreshTargetAsync(ProgramsConstants.TargetKinds.LearningPath, progress.PathId, progress.UserId, cancellationToken);
        }
    }
}
