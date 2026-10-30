using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Documents.Services;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Documents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Documents.Endpoints;

/// <summary>
/// An AI profile's knowledge documents over the API: list them, upload more, and remove one. Uploads are chunked,
/// embedded and indexed as they are from the profile's editor.
/// </summary>
internal static class AIProfileDocumentsApiEndpoints
{
    public static IEndpointRouteBuilder MapAIProfileDocumentsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("api/ai/v1/profiles/{profileId}/documents")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AIApiKeys.ApiSchemes })
            .DisableAntiforgery()
            .WithTags("AI Knowledge");

        group.MapGet(string.Empty, ListAsync)
            .WithName("ListAIProfileDocuments")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Lists the documents an AI profile answers from.");

        group.MapPost(string.Empty, UploadAsync)
            .WithName("UploadAIProfileDocuments")
            .Accepts<IFormFileCollection>("multipart/form-data")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .WithSummary("Uploads documents for an AI profile to answer from, as multipart form files.");

        group.MapDelete("{documentId}", DeleteAsync)
            .WithName("DeleteAIProfileDocument")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Removes a document from an AI profile and from the index.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiAIDocument>>, ProblemHttpResult>> ListAsync(
        string profileId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Knowledge, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var profile = await profileManager.FindByIdAsync(profileId, httpContext.RequestAborted);

        if (profile is null)
        {
            return NotFound();
        }

        var documents = profile.TryGet<DocumentsMetadata>(out var metadata) ? metadata.Documents ?? [] : [];

        return TypedResults.Ok<IReadOnlyList<ApiAIDocument>>(documents.Select(ToApi).ToList());
    }

    private static async Task<Results<Ok<ApiAIDocumentUpload>, ValidationProblem, ProblemHttpResult>> UploadAsync(
        string profileId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory,
        IAIDocumentProcessingService processingService,
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore,
        IOptions<ChatDocumentsOptions> options,
        ILoggerFactory loggerFactory)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Knowledge, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var profile = await profileManager.FindByIdAsync(profileId, httpContext.RequestAborted);

        if (profile is null)
        {
            return NotFound();
        }

        if (!httpContext.Request.HasFormContentType)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["files"] = ["Send the files as multipart/form-data."] });
        }

        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);

        if (form.Files.Count == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["files"] = ["Send at least one file."] });
        }

        var metadata = profile.GetOrCreate<DocumentsMetadata>();
        metadata.Documents ??= [];

        var chatDeployment = await deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: profile.ChatDeploymentName);
        var embeddingDeployment = await deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Embedding, clientName: chatDeployment?.ClientName);
        var embeddingGenerator = embeddingDeployment is null
            ? null
            : await clientFactory.CreateEmbeddingGeneratorAsync(embeddingDeployment, builder => builder.UseDefaultResilience().UseUsageLabels(purpose: AIUsagePurposes.Indexing));

        var logger = loggerFactory.CreateLogger(typeof(AIProfileDocumentsApiEndpoints));
        var added = new List<ApiAIDocument>();
        var errors = new List<string>();
        var processed = new List<AIDocument>();

        foreach (var file in form.Files)
        {
            if (file is null || file.Length == 0)
            {
                continue;
            }

            var extension = Path.GetExtension(file.FileName);

            if (!options.Value.EmbeddableFileExtensions.Contains(extension))
            {
                errors.Add($"{file.FileName}: the file type '{extension}' is not supported for profile documents.");

                continue;
            }

            try
            {
                var result = await processingService.ProcessFileAsync(
                    file,
                    profile.ItemId,
                    AIConstants.DocumentReferenceTypes.Profile,
                    embeddingGenerator,
                    metadata.MaxIndexableCharacters,
                    metadata.DescribeFiguresInUploads);

                if (!result.Success)
                {
                    errors.Add($"{file.FileName}: {result.Error}");

                    continue;
                }

                metadata.Documents.Add(result.DocumentInfo);
                await documentStore.CreateAsync(result.Document);

                foreach (var chunk in result.Chunks)
                {
                    await chunkStore.CreateAsync(chunk);
                }

                processed.Add(result.Document);
                added.Add(ToApi(result.DocumentInfo));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The API could not process the uploaded document for profile '{ProfileId}'.", profile.ItemId);
                errors.Add($"{file.FileName}: it could not be processed.");
            }
        }

        if (processed.Count > 0)
        {
            profile.Put(metadata);
            await profileManager.UpdateAsync(profile, cancellationToken: httpContext.RequestAborted);
            ProfileDocumentChunkIndexer.ScheduleIndexing(processed);
        }

        return TypedResults.Ok(new ApiAIDocumentUpload(added, errors));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        string profileId,
        string documentId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Knowledge, AIPermissions.ManageAIProfiles))
        {
            return Forbidden();
        }

        var profile = await profileManager.FindByIdAsync(profileId, httpContext.RequestAborted);
        var metadata = profile is not null && profile.TryGet<DocumentsMetadata>(out var existing) ? existing : null;
        var info = metadata?.Documents?.FirstOrDefault(document => document.DocumentId == documentId);

        if (info is null)
        {
            return NotFound();
        }

        var chunkIds = new List<string>();

        if (await documentStore.FindByIdAsync(documentId) is { } document)
        {
            chunkIds.AddRange((await chunkStore.GetChunksByAIDocumentIdAsync(document.ItemId)).Select(chunk => chunk.ItemId));
            await chunkStore.DeleteByDocumentIdAsync(document.ItemId);
            await documentStore.DeleteAsync(document);
        }

        metadata.Documents.Remove(info);
        profile.Put(metadata);
        await profileManager.UpdateAsync(profile, cancellationToken: httpContext.RequestAborted);

        if (chunkIds.Count > 0)
        {
            ShellScope.AddDeferredTask(scope => RemoveChunksAsync(scope, chunkIds));
        }

        return TypedResults.NoContent();
    }

    private static async Task RemoveChunksAsync(ShellScope scope, List<string> chunkIds)
    {
        var services = scope.ServiceProvider;
        var indexProfiles = await services.GetRequiredService<IIndexProfileStore>().GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        foreach (var indexProfile in indexProfiles)
        {
            if (services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName) is { } indexManager)
            {
                await indexManager.DeleteDocumentsAsync(indexProfile, chunkIds);
            }
        }
    }

    private static ApiAIDocument ToApi(ChatDocumentInfo document)
        => new(document.DocumentId, document.FileName, document.ContentType, document.FileSize);

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    private static ProblemHttpResult NotFound()
        => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found");
}

/// <summary>
/// A document an AI profile answers from.
/// </summary>
/// <param name="Id">The document.</param>
/// <param name="FileName">Its file name.</param>
/// <param name="ContentType">Its content type.</param>
/// <param name="Size">Its size in bytes.</param>
public sealed record ApiAIDocument(string Id, string FileName, string ContentType, long Size);

/// <summary>
/// What came of an upload.
/// </summary>
/// <param name="Added">The documents added.</param>
/// <param name="Errors">The files that were not, and why.</param>
public sealed record ApiAIDocumentUpload(IReadOnlyList<ApiAIDocument> Added, IReadOnlyList<string> Errors);
