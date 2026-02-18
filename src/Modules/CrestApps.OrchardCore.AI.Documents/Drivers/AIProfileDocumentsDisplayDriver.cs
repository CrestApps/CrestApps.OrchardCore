using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Documents.Services;
using CrestApps.OrchardCore.AI.Documents.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

internal sealed class AIProfileDocumentsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ISiteService _siteService;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIDocumentProcessingService _documentProcessingService;
    private readonly IOptions<ChatDocumentsOptions> _extractorOptions;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public AIProfileDocumentsDisplayDriver(
        ISiteService siteService,
        IIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider,
        IAIDocumentStore documentStore,
        IAIDocumentProcessingService documentProcessingService,
        IOptions<ChatDocumentsOptions> extractorOptions,
        ILogger<AIProfileDocumentsDisplayDriver> logger,
        IStringLocalizer<AIProfileDocumentsDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
        _documentStore = documentStore;
        _documentProcessingService = documentProcessingService;
        _extractorOptions = extractorOptions;
        _logger = logger;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileDocumentsViewModel>("AIProfileDocuments_Edit", async model =>
        {
            model.ProfileId = profile.ItemId;

            var documentsMetadata = profile.As<AIProfileDocumentsMetadata>();
            model.Documents = documentsMetadata.Documents ?? [];
            model.TopN = documentsMetadata.DocumentTopN ?? 3;

            var settings = await _siteService.GetSettingsAsync<InteractionDocumentSettings>();
            model.IndexProfileName = settings.IndexProfileName;
            model.HasIndexProfile = !string.IsNullOrEmpty(settings.IndexProfileName);

            if (model.HasIndexProfile)
            {
                var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);
                if (indexProfile != null)
                {
                    var searchService = _serviceProvider.GetKeyedService<IVectorSearchService>(indexProfile.ProviderName);
                    model.HasVectorSearchService = searchService != null;
                }
            }
        }).Location("Content:5#Documents:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileDocumentsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var documentsMetadata = profile.As<AIProfileDocumentsMetadata>();
        documentsMetadata.DocumentTopN = model.TopN > 0 ? model.TopN : 3;
        documentsMetadata.Documents ??= [];

        if (context.Updater.ModelState.IsValid)
        {
            // Handle document removals.
            if (model.RemovedDocumentIds != null && model.RemovedDocumentIds.Length > 0)
            {
                foreach (var documentId in model.RemovedDocumentIds)
                {
                    if (string.IsNullOrEmpty(documentId))
                    {
                        continue;
                    }

                    var document = await _documentStore.FindByIdAsync(documentId);

                    if (document != null)
                    {
                        await _documentStore.DeleteAsync(document);
                    }

                    var docInfo = documentsMetadata.Documents.FirstOrDefault(d => d.DocumentId == documentId);

                    if (docInfo != null)
                    {
                        documentsMetadata.Documents.Remove(docInfo);
                    }
                }
            }

            // Handle file uploads.
            if (model.Files != null && model.Files.Length > 0)
            {
                var embeddingGenerator = await _documentProcessingService.CreateEmbeddingGeneratorAsync(profile.Source, profile.ConnectionName);

                foreach (var file in model.Files)
                {
                    if (file == null || file.Length == 0)
                    {
                        continue;
                    }

                    var extension = Path.GetExtension(file.FileName);

                    if (!_extractorOptions.Value.EmbeddableFileExtensions.Contains(extension))
                    {
                        context.Updater.ModelState.AddModelError(
                            Prefix + "." + nameof(model.Files),
                            S["File type '{0}' is not supported for AI Profile documents. Only text-based files are allowed.", extension]);
                        continue;
                    }

                    try
                    {
                        var result = await _documentProcessingService.ProcessFileAsync(
                            file,
                            profile.ItemId,
                            AIConstants.DocumentReferenceTypes.Profile,
                            embeddingGenerator);

                        if (!result.Success)
                        {
                            context.Updater.ModelState.AddModelError(
                                Prefix + "." + nameof(model.Files),
                                S["{0}: {1}", file.FileName, result.Error]);
                            continue;
                        }

                        documentsMetadata.Documents.Add(result.DocumentInfo);
                        await _documentStore.CreateAsync(result.Document);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process file {FileName}", file.FileName);
                        context.Updater.ModelState.AddModelError(
                            Prefix + "." + nameof(model.Files),
                            S["Failed to process file '{0}'.", file.FileName]);
                    }
                }
            }
        }

        profile.Put(documentsMetadata);

        return Edit(profile, context);
    }
}
