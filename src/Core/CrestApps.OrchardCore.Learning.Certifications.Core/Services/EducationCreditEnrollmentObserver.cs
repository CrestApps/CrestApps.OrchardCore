using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// Awards the education credits of a course when an enrollment completes it, once per enrollment.
/// </summary>
public sealed class EducationCreditEnrollmentObserver : IEnrollmentObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public EducationCreditEnrollmentObserver(IServiceProvider services, ILogger<EducationCreditEnrollmentObserver> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        try
        {
            var course = await _services.GetRequiredService<IContentManager>().GetAsync(enrollment.CourseId, VersionOptions.Published);

            if (course is null || !course.TryGet<EducationCreditsPart>(out var part) || part.Awards.Count == 0)
            {
                return;
            }

            var completedUtc = enrollment.CompletedUtc ?? _services.GetRequiredService<IClock>().UtcNow;
            var localClock = _services.GetRequiredService<ILocalClock>();
            var earnedOn = DateOnly.FromDateTime((await localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(completedUtc, DateTimeKind.Utc)))).DateTime);

            await EducationCreditAwards.AwardAsync(_services.GetRequiredService<IEducationCreditService>(), part, enrollment.UserId, earnedOn, CertificationsConstants.CreditSources.Course, enrollment.ItemId, course.DisplayText, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The education credits of a completed course could not be awarded.");
        }
    }
}

/// <summary>
/// Awards the credits of an education credits part.
/// </summary>
public static class EducationCreditAwards
{
    /// <summary>
    /// Awards each credit of a part to a person, once per source record.
    /// </summary>
    public static async Task AwardAsync(IEducationCreditService credits, EducationCreditsPart part, string userId, DateOnly earnedOn, string source, string sourceId, string note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credits);
        ArgumentNullException.ThrowIfNull(part);

        foreach (var award in part.Awards.Where(award => award.Amount > 0 && !string.IsNullOrEmpty(award.CreditTypeId)))
        {
            await credits.AwardAsync(new EducationCreditAwardRequest
            {
                UserId = userId,
                CreditTypeId = award.CreditTypeId,
                Amount = award.Amount,
                EarnedOn = earnedOn,
                ExpiresOn = award.ValidityMonths is { } months and > 0 ? earnedOn.AddMonths(months) : null,
                Source = source,
                SourceId = sourceId,
                Note = note,
            }, cancellationToken);
        }
    }
}
