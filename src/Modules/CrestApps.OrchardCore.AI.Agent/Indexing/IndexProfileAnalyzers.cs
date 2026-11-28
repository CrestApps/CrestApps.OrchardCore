using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Elasticsearch;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Lucene;
using OrchardCore.Lucene.Models;
using OrchardCore.Lucene.Services;

namespace CrestApps.OrchardCore.AI.Agent.Indexing;

/// <summary>
/// The analyzer of an index profile, which the admin editor of a Lucene or Elasticsearch index always sets and the
/// index profile handlers only copy when one is given.
/// </summary>
internal static class IndexProfileAnalyzers
{
    /// <summary>
    /// Returns the analyzer the provider's admin editor gives a new index when none is chosen, or <see langword="null"/>
    /// for a provider without analyzers.
    /// </summary>
    public static string GetDefault(string providerName)
    {
        if (IsLucene(providerName))
        {
            return LuceneConstants.DefaultAnalyzer;
        }

        if (string.Equals(providerName, ElasticsearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return ElasticsearchConstants.DefaultAnalyzer;
        }

        return null;
    }

    /// <summary>
    /// Returns whether a Lucene index profile has no analyzer the site knows. Lucene's search builds its query parser
    /// with that analyzer and throws a <see cref="NullReferenceException"/> without one. Elasticsearch falls back to its
    /// default analyzer, so only Lucene profiles are checked.
    /// </summary>
    public static bool IsMissing(IServiceProvider services, IndexProfile indexProfile)
    {
        if (!IsLucene(indexProfile.ProviderName))
        {
            return false;
        }

        var analyzerName = indexProfile.TryGet<LuceneIndexMetadata>(out var metadata)
            ? metadata.AnalyzerName
            : null;

        if (string.IsNullOrEmpty(analyzerName))
        {
            return true;
        }

        var analyzerManager = services.GetService<LuceneAnalyzerManager>();

        return analyzerManager is not null &&
            !analyzerManager.GetAnalyzers().Any(analyzer => string.Equals(analyzer.Name, analyzerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gives a Lucene index profile without an analyzer the site knows the default one, as its admin editor does.
    /// Returns whether the profile was changed.
    /// </summary>
    public static bool SetDefaultWhenMissing(IServiceProvider services, IndexProfile indexProfile)
    {
        if (!IsMissing(services, indexProfile))
        {
            return false;
        }

        indexProfile.Alter<LuceneIndexMetadata>(metadata => metadata.AnalyzerName = LuceneConstants.DefaultAnalyzer);

        return true;
    }

    private static bool IsLucene(string providerName)
        => string.Equals(providerName, LuceneConstants.ProviderName, StringComparison.OrdinalIgnoreCase);
}
