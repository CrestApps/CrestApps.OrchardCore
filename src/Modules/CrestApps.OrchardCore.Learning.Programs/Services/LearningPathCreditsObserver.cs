using CrestApps.OrchardCore.Learning.Certifications;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Awards the education credits of a learning path when a person completes it, once per path progress.
/// </summary>
internal sealed class LearningPathCreditsObserver : ILearningPathObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public LearningPathCreditsObserver(IServiceProvider services, ILogger<LearningPathCreditsObserver> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task CompletedAsync(LearningPathProgress progress, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = await _services.GetRequiredService<IContentManager>().GetAsync(progress.PathId, VersionOptions.Published);

            if (path is null || !path.TryGet<EducationCreditsPart>(out var part) || part.Awards.Count == 0)
            {
                return;
            }

            var completedUtc = progress.CompletedUtc ?? _services.GetRequiredService<IClock>().UtcNow;
            var local = await _services.GetRequiredService<ILocalClock>().ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(completedUtc, DateTimeKind.Utc)));

            await EducationCreditAwards.AwardAsync(_services.GetRequiredService<IEducationCreditService>(), part, progress.UserId, DateOnly.FromDateTime(local.DateTime), CertificationsConstants.CreditSources.LearningPath, progress.ItemId, path.DisplayText, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The education credits of a completed learning path could not be awarded.");
        }
    }
}
