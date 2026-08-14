using System.ComponentModel.DataAnnotations;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

internal sealed class SearchIndexProfileAIDataSourceSourceHandler : IAIDataSourceSourceHandler
{
    private readonly IIndexProfileManager _indexProfileManager;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchIndexProfileAIDataSourceSourceHandler"/> class.
    /// </summary>
    /// <param name="indexProfileManager">The index profile manager.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public SearchIndexProfileAIDataSourceSourceHandler(
        IIndexProfileManager indexProfileManager,
        IServiceProvider serviceProvider)
    {
        _indexProfileManager = indexProfileManager;
        _serviceProvider = serviceProvider;
    }

    public string SourceType => AIDataSourceSourceTypes.SearchIndexProfile;

    public async ValueTask ValidateAsync(
        AIDataSource dataSource,
        ValidationResultDetails result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(result);

        if (string.IsNullOrWhiteSpace(dataSource.SourceIndexProfileName))
        {
            result.Fail(new ValidationResult("Source index profile is required.", [nameof(AIDataSource.SourceIndexProfileName)]));

            return;
        }

        var sourceProfile = await ResolveSourceProfileAsync(dataSource);

        if (sourceProfile == null)
        {
            result.Fail(new ValidationResult("The selected source index profile could not be found.", [nameof(AIDataSource.SourceIndexProfileName)]));

            return;
        }

        if (string.Equals(sourceProfile.Type, AIConstants.AIDocumentsIndexingTaskType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourceProfile.Type, MemoryConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourceProfile.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
        {
            result.Fail(new ValidationResult("The selected source index profile type is not supported for data sources.", [nameof(AIDataSource.SourceIndexProfileName)]));
        }

        var documentReader = _serviceProvider.GetKeyedService<IDataSourceDocumentReader>(sourceProfile.ProviderName);

        if (documentReader == null)
        {
            result.Fail(new ValidationResult("The selected source index provider cannot read source documents.", [nameof(AIDataSource.SourceIndexProfileName)]));
        }
    }

    public async ValueTask<string> GetReferenceTypeAsync(
        AIDataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        var sourceProfile = await ResolveSourceProfileAsync(dataSource);

        return sourceProfile?.Type ?? AIConstants.DataSourceReferenceTypes.Content;
    }

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        AIDataSource dataSource,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (sourceProfile, documentReader) = await ResolveRequiredAsync(dataSource);

        await foreach (var pair in documentReader.ReadAsync(
            sourceProfile.ToIndexProfileInfo(),
            dataSource.KeyFieldName,
            dataSource.TitleFieldName,
            dataSource.ContentFieldName,
            cancellationToken))
        {
            yield return pair;
        }
    }

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadByIdsAsync(
        AIDataSource dataSource,
        IEnumerable<string> documentIds,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (sourceProfile, documentReader) = await ResolveRequiredAsync(dataSource);

        await foreach (var pair in documentReader.ReadByIdsAsync(
            sourceProfile.ToIndexProfileInfo(),
            documentIds,
            dataSource.KeyFieldName,
            dataSource.TitleFieldName,
            dataSource.ContentFieldName,
            cancellationToken))
        {
            yield return pair;
        }
    }

    private async Task<(IndexProfile SourceProfile, IDataSourceDocumentReader DocumentReader)> ResolveRequiredAsync(
        AIDataSource dataSource)
    {
        var sourceProfile = await ResolveSourceProfileAsync(dataSource);
        var documentReader = sourceProfile == null
            ? null
            : _serviceProvider.GetKeyedService<IDataSourceDocumentReader>(sourceProfile.ProviderName);

        return sourceProfile == null || documentReader == null
            ? throw new InvalidOperationException("The configured Search Index Profile data source could not be resolved.")
            : (sourceProfile, documentReader);
    }

    private async Task<IndexProfile> ResolveSourceProfileAsync(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        if (string.IsNullOrWhiteSpace(dataSource.SourceIndexProfileName))
        {
            return null;
        }

        return await _indexProfileManager.FindByNameAsync(dataSource.SourceIndexProfileName);
    }
}
