using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardDocumentIndex = OrchardCore.Indexing.DocumentIndex;

namespace CrestApps.OrchardCore.AI.Core.Services;

internal sealed class OrchardCoreSearchDocumentManager : ISearchDocumentManager
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;

    public OrchardCoreSearchDocumentManager(
        IIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider)
    {
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> AddOrUpdateAsync(
        IIndexProfileInfo profile,
        IReadOnlyCollection<IndexDocument> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var indexProfile = await ResolveIndexProfileAsync(profile);
        var manager = ResolveDocumentIndexManager(indexProfile.ProviderName);
        var orchardDocuments = documents.Select(ConvertDocument).ToArray();

        return await manager.AddOrUpdateDocumentsAsync(indexProfile, orchardDocuments);
    }

    public async Task DeleteAsync(
        IIndexProfileInfo profile,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentIds);

        var indexProfile = await ResolveIndexProfileAsync(profile);
        var manager = ResolveDocumentIndexManager(indexProfile.ProviderName);

        await manager.DeleteDocumentsAsync(indexProfile, documentIds);
    }

    public async Task DeleteAllAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default)
    {
        var indexProfile = await ResolveIndexProfileAsync(profile);
        var manager = ResolveDocumentIndexManager(indexProfile.ProviderName);

        await manager.DeleteAllDocumentsAsync(indexProfile);
    }

    private static OrchardDocumentIndex ConvertDocument(IndexDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var orchardDocument = new OrchardDocumentIndex(document.Id);

        foreach (var (name, value) in document.Fields)
        {
            switch (value)
            {
                case null:
                    continue;
                case string stringValue:
                    orchardDocument.Set(name, stringValue, DocumentIndexOptions.Store);
                    break;
                case IHtmlContent htmlValue:
                    orchardDocument.Set(name, htmlValue, DocumentIndexOptions.Store);
                    break;
                case DateTimeOffset dateTimeOffsetValue:
                    orchardDocument.Set(name, dateTimeOffsetValue, DocumentIndexOptions.Store);
                    break;
                case DateTime dateTimeValue:
                    orchardDocument.Set(name, new DateTimeOffset(dateTimeValue), DocumentIndexOptions.Store);
                    break;
                case int intValue:
                    orchardDocument.Set(name, intValue, DocumentIndexOptions.Store);
                    break;
                case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                    orchardDocument.Set(name, (int)longValue, DocumentIndexOptions.Store);
                    break;
                case short shortValue:
                    orchardDocument.Set(name, (int)shortValue, DocumentIndexOptions.Store);
                    break;
                case byte byteValue:
                    orchardDocument.Set(name, (int)byteValue, DocumentIndexOptions.Store);
                    break;
                case bool boolValue:
                    orchardDocument.Set(name, boolValue, DocumentIndexOptions.Store);
                    break;
                case float floatValue:
                    orchardDocument.Set(name, (double)floatValue, DocumentIndexOptions.Store);
                    break;
                case double doubleValue:
                    orchardDocument.Set(name, doubleValue, DocumentIndexOptions.Store);
                    break;
                case decimal decimalValue:
                    orchardDocument.Set(name, decimalValue, DocumentIndexOptions.Store);
                    break;
                case float[] vectorValue:
                    orchardDocument.Set(name, vectorValue, vectorValue.Length, DocumentIndexOptions.Store);
                    break;
                default:
                    orchardDocument.Set(name, value, DocumentIndexOptions.Store);
                    break;
            }
        }

        return orchardDocument;
    }

    private async ValueTask<IndexProfile> ResolveIndexProfileAsync(IIndexProfileInfo profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        IndexProfile indexProfile = null;

        if (!string.IsNullOrWhiteSpace(profile.IndexProfileId))
        {
            indexProfile = await _indexProfileStore.FindByIdAsync(profile.IndexProfileId);
        }

        if (indexProfile == null &&
            !string.IsNullOrWhiteSpace(profile.IndexName) &&
            !string.IsNullOrWhiteSpace(profile.ProviderName))
        {
            indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(profile.IndexName, profile.ProviderName);
        }

        if (indexProfile == null)
        {
            throw new InvalidOperationException(
                $"The Orchard Core index profile '{profile.IndexProfileId ?? profile.IndexName}' for provider '{profile.ProviderName}' could not be found.");
        }

        return indexProfile;
    }

    private IDocumentIndexManager ResolveDocumentIndexManager(string providerName)
        => _serviceProvider.GetRequiredKeyedService<IDocumentIndexManager>(providerName);
}
