using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Finds what an embedded survey links to: its survey, the published version and an open collector. Invitation and
/// kiosk collectors are never embedded: an invitation link is personal, and a kiosk link takes unlimited guest answers,
/// which on a public page would bypass the survey's per-person limits.
/// </summary>
public sealed class SurveyEmbedResolver
{
    private readonly SurveyStore _surveyStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly ISurveyVersionCache _versionCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyEmbedResolver"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="versionCache">The version cache.</param>
    public SurveyEmbedResolver(
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        ISurveyVersionCache versionCache)
    {
        _surveyStore = surveyStore;
        _collectorStore = collectorStore;
        _versionCache = versionCache;
    }

    /// <summary>
    /// Resolves a survey by id or technical name and a collector by id or token.
    /// </summary>
    /// <param name="surveyIdOrName">The survey's id or technical name.</param>
    /// <param name="collectorIdOrToken">The collector's id or token, or <see langword="null"/> for the default link.</param>
    /// <returns>The survey, its published version and the collector, each <see langword="null"/> when not found.</returns>
    public async Task<(Survey Survey, SurveyVersion Version, SurveyCollector Collector)> ResolveAsync(string surveyIdOrName, string collectorIdOrToken)
    {
        if (string.IsNullOrWhiteSpace(surveyIdOrName))
        {
            return (null, null, null);
        }

        var survey = await _surveyStore.FindByIdAsync(surveyIdOrName.Trim()) ?? await _surveyStore.FindByNameAsync(surveyIdOrName);

        if (survey is null || survey.Status != SurveyStatus.Open)
        {
            return (survey, null, null);
        }

        var version = await _versionCache.GetPublishedAsync(survey);
        SurveyCollector collector = null;

        if (!string.IsNullOrWhiteSpace(collectorIdOrToken))
        {
            collector = await _collectorStore.FindByIdAsync(collectorIdOrToken.Trim()) ?? await _collectorStore.FindByTokenAsync(collectorIdOrToken.Trim());
        }

        collector ??= await _collectorStore.FindDefaultAsync(survey.ItemId);

        if (collector is null || collector.SurveyId != survey.ItemId || collector.Status != CollectorStatus.Open || !CanEmbed(collector))
        {
            collector = null;
        }

        return (survey, version, collector);
    }

    /// <summary>
    /// Gets whether a collector's link may be embedded in a page: not an invitation or kiosk collector.
    /// </summary>
    /// <param name="collector">The collector.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    public static bool CanEmbed(SurveyCollector collector)
        => collector is not null && collector.Kind is not SurveyCollectorKinds.Invitation and not SurveyCollectorKinds.Kiosk;
}
