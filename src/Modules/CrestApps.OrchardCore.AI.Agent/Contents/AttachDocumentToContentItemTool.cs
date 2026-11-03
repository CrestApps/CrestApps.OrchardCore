using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Media.Fields;
using OrchardCore.Media.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// AI tool that attaches a document of the current conversation, uploaded or generated, to a media field of a content item.
/// </summary>
public sealed class AttachDocumentToContentItemTool : AIFunction
{
    /// <summary>
    /// The name constant.
    /// </summary>
    public const string TheName = "attachDocumentToContentItem";

    // The folder Orchard Core's "Attached" media field editor keeps a content item's files in, so the field finds them there too.
    private const string MediaFieldsFolder = "mediafields";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ContentItemId of the content item to attach the document to."
        },
        "documentId": {
          "type": "string",
          "description": "The id of the document to attach. Omit it when only one document is available in the conversation."
        },
        "partName": {
          "type": "string",
          "description": "The name of the content part that holds the media field. Omit it when the content type has only one media field."
        },
        "fieldName": {
          "type": "string",
          "description": "The name of the media field to attach the document to. Omit it when the content type has only one media field."
        },
        "mediaText": {
          "type": "string",
          "description": "Optional text to show with the file, such as a caption or alternate text."
        },
        "replaceExisting": {
          "type": "boolean",
          "description": "When true, the document replaces the files already in the field instead of being added to them. A field that holds a single file must be replaced explicitly."
        },
        "isDraft": {
          "type": "boolean",
          "description": "When true, the change is saved as a draft. When false, the content item is published. When omitted, a published item is published again and an item with an unpublished draft stays a draft."
        }
      },
      "required": [
        "contentItemId"
      ],
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "Attaches a document of the current conversation, such as a file the user uploaded or a file you generated, to a media field of an existing content item. The file is saved to the media library.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var logger = services.GetRequiredService<ILogger<AttachDocumentToContentItemTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            logger.LogWarning("AI tool '{ToolName}': Unable to find a contentItemId argument in the function arguments.", TheName);

            return "Unable to find a contentItemId argument in the function arguments.";
        }

        arguments.TryGetFirstString("documentId", out var documentId);
        arguments.TryGetFirstString("partName", out var partName);
        arguments.TryGetFirstString("fieldName", out var fieldName);
        arguments.TryGetFirstString("mediaText", out var mediaText);
        arguments.TryGetFirst<bool>("replaceExisting", out var replaceExisting);
        bool? isDraft = arguments.TryGetFirst<bool>("isDraft", out var draft) ? draft : null;

        // Only the documents of the current conversation may be attached, never any document the id of which the model knows.
        var documents = await GetConversationDocumentsAsync(services);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}': Found {DocumentCount} documents in the current conversation.", TheName, documents.Count);
        }

        if (documents.Count == 0)
        {
            logger.LogWarning("AI tool '{ToolName}': Refused because the current conversation has no documents.", TheName);

            return "No documents are available in the current conversation. Ask the user to upload the document first.";
        }

        var document = SelectDocument(documents, documentId);

        if (document is null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("AI tool '{ToolName}': No document selected. Requested id: '{DocumentId}'. Documents in the conversation: {Documents}.", TheName, documentId, FormatDocuments(documents));
            }

            return string.IsNullOrWhiteSpace(documentId)
                ? $"Several documents are available. Call this tool again with the 'documentId' of the one to attach: {FormatDocuments(documents)}"
                : $"No document with the id '{documentId}' is available in the current conversation. Available documents: {FormatDocuments(documents)}";
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}': Selected document '{DocumentId}', file '{FileName}', {FileSize} bytes.", TheName, document.ItemId, document.FileName, document.FileSize);
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var latestContentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (latestContentItem is null)
        {
            logger.LogWarning("AI tool '{ToolName}': Unable to find a content item with ContentItemId '{ContentItemId}'.", TheName, contentItemId);

            return $"Unable to find a content item that matches the ContentItemId: {contentItemId}";
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}': Loaded content item '{ContentItemId}' of type '{ContentType}', version '{VersionId}', published: {Published}, latest: {Latest}.", TheName, contentItemId, latestContentItem.ContentType, latestContentItem.ContentItemVersionId, latestContentItem.Published, latestContentItem.Latest);
        }

        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var mediaFields = GetMediaFields(await contentDefinitionManager.GetTypeDefinitionAsync(latestContentItem.ContentType));

        if (mediaFields.Count == 0)
        {
            logger.LogWarning("AI tool '{ToolName}': Refused because content type '{ContentType}' has no media field.", TheName, latestContentItem.ContentType);

            return $"The content type '{latestContentItem.ContentType}' has no media field to attach a document to. A MediaField has to be added to the content type first.";
        }

        var mediaField = SelectMediaField(mediaFields, partName, fieldName);

        if (mediaField is null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("AI tool '{ToolName}': No media field selected. Requested part: '{PartName}', field: '{FieldName}'. Media fields: {MediaFields}.", TheName, partName, fieldName, string.Join(", ", mediaFields));
            }

            return string.IsNullOrWhiteSpace(partName) && string.IsNullOrWhiteSpace(fieldName)
                ? $"The content type '{latestContentItem.ContentType}' has several media fields. Ask the user which one to use, then call this tool again with its 'partName' and 'fieldName': {string.Join(", ", mediaFields)}"
                : $"The content type '{latestContentItem.ContentType}' has no such media field. Its media fields are: {string.Join(", ", mediaFields)}";
        }

        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();
        var folder = mediaFileStore.Combine(MediaFieldsFolder, latestContentItem.ContentType, latestContentItem.ContentItemId);

        if (!await IsAuthorizedAsync(services, logger, latestContentItem, folder))
        {
            logger.LogWarning("AI tool '{ToolName}': The current user is not allowed to attach files to content item '{ContentItemId}'.", TheName, contentItemId);

            return $"The current user is not allowed to edit content item '{contentItemId}' or to add files to the media library.";
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}': Permission checks passed for content item '{ContentItemId}'. Selected media field '{MediaField}'.", TheName, contentItemId, mediaField);
        }

        var fileName = GetMediaFileName(services, document.FileName);
        var extension = Path.GetExtension(fileName);
        var mediaOptions = services.GetRequiredService<IOptions<MediaOptions>>().Value;

        if (!mediaOptions.AllowedFileExtensions.Contains(extension))
        {
            logger.LogWarning("AI tool '{ToolName}': Refused '{FileName}' because the media library does not accept '{Extension}' files.", TheName, document.FileName, extension);

            return $"The media library does not accept '{extension}' files, so '{document.FileName}' cannot be attached.";
        }

        var settings = mediaField.Definition.GetSettings<MediaFieldSettings>();

        if (settings.AllowedExtensions?.Length > 0 && !settings.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning("AI tool '{ToolName}': Refused '{FileName}' because field '{MediaField}' does not accept '{Extension}' files.", TheName, document.FileName, mediaField, extension);

            return $"The '{mediaField}' field only accepts {string.Join(", ", settings.AllowedExtensions)} files, so '{document.FileName}' cannot be attached.";
        }

        if (mediaOptions.MaxFileSize > 0 && document.FileSize > mediaOptions.MaxFileSize)
        {
            logger.LogWarning("AI tool '{ToolName}': Refused '{FileName}' because its {FileSize} bytes exceed the media library's limit of {MaxFileSize} bytes.", TheName, document.FileName, document.FileSize, mediaOptions.MaxFileSize);

            return $"'{document.FileName}' is larger than the {mediaOptions.MaxFileSize} bytes the media library accepts.";
        }

        var currentPaths = latestContentItem.Get<ContentPart>(mediaField.PartName)?.Get<MediaField>(mediaField.FieldName)?.Paths ?? [];

        if (!settings.Multiple && currentPaths.Length > 0 && !replaceExisting)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("AI tool '{ToolName}': Field '{MediaField}' holds a single file and already has one, and replaceExisting was not passed.", TheName, mediaField);
            }

            return $"The '{mediaField}' field holds a single file and already has one. Ask the user whether to replace it, then call this tool again with 'replaceExisting' set to true.";
        }

        if (string.IsNullOrEmpty(document.StoredFilePath))
        {
            logger.LogWarning("AI tool '{ToolName}': Refused document '{DocumentId}' because its file is not stored.", TheName, document.ItemId);

            return $"The file of the document '{document.FileName}' is not stored, so it cannot be attached.";
        }

        var documentFileStore = services.GetRequiredService<IDocumentFileStore>();
        var fileCreationService = services.GetRequiredService<FileCreationService>();
        string mediaPath;

        await using (var stream = await documentFileStore.GetFileAsync(document.StoredFilePath))
        {
            if (stream is null)
            {
                logger.LogWarning("AI tool '{ToolName}': The file of document '{DocumentId}' was not found at '{Path}'.", TheName, document.ItemId, document.StoredFilePath);

                return $"The file of the document '{document.FileName}' could not be found.";
            }

            mediaPath = await GetAvailableMediaPathAsync(mediaFileStore, folder, fileName);

            // Every handler (an antivirus scanner, for one) sees the file before it reaches the media library, and any of them
            // can refuse it. An uploaded document was scanned when it was uploaded, but a generated one never was.
            await using var creation = await fileCreationService.CreateAsync(
                new FileCreatingContext(mediaPath, document.FileSize > 0 ? document.FileSize : null, document.ContentType),
                stream,
                leaveOpen: true,
                cancellationToken);

            if (!creation.Succeeded)
            {
                logger.LogWarning("AI tool '{ToolName}': The file of document '{DocumentId}' was refused before it reached the media library: {Reason}", TheName, document.ItemId, creation.ErrorMessage);

                return $"The file '{document.FileName}' was refused before it could be saved to the media library: {creation.ErrorMessage}";
            }

            await mediaFileStore.TryCreateDirectoryAsync(folder);
            mediaPath = await mediaFileStore.CreateFileFromStreamAsync(mediaPath, creation.Stream);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}': Saved the file to the media library at '{MediaPath}'.", TheName, mediaPath);
        }

        // For a type without versions, the draft is this same item and loading it as a draft unpublishes it.
        // Its state is kept to publish it again afterwards, or to put it back if the file is not attached.
        var wasPublished = latestContentItem.Published;

        try
        {
            var mediaFileInfo = await mediaFileStore.GetFileInfoAsync(mediaPath);

            if (mediaFileInfo is not null)
            {
                await fileCreationService.CreatedAsync(mediaFileInfo, cancellationToken);
            }

            var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}': Loaded the draft: version '{VersionId}', published: {Published}, same item as the latest version (a type without versions): {SameItem}.", TheName, contentItem.ContentItemVersionId, contentItem.Published, ReferenceEquals(contentItem, latestContentItem));
            }

            var previousField = contentItem.Get<ContentPart>(mediaField.PartName)?.Get<MediaField>(mediaField.FieldName);
            var previous = new MediaFieldValues(previousField?.Paths ?? [], previousField?.MediaTexts ?? [], GetAttachedFileNames(previousField));

            // Orchard Core's editor pairs every path with a media text, and the "Attached" editor with a file name too,
            // so the arrays stay the same length.
            var displayName = Path.GetFileName((document.FileName ?? fileName).Replace('\\', '/'));
            var updated = replaceExisting || !settings.Multiple
                ? new MediaFieldValues([mediaPath], [mediaText ?? string.Empty], [displayName])
                : new MediaFieldValues(
                    [.. previous.Paths, mediaPath],
                    [.. Align(previous.Texts, previous.Paths.Length), mediaText ?? string.Empty],
                    [.. Align(previous.AttachedFileNames, previous.Paths.Length), displayName]);

            var isAttachedEditor = string.Equals(mediaField.Definition.Editor(), "Attached", StringComparison.OrdinalIgnoreCase);

            SetMediaField(contentItem, mediaField, updated, isAttachedEditor);

            await contentManager.UpdateAsync(contentItem);

            var result = await contentManager.ValidateAsync(contentItem);

            if (!result.Succeeded)
            {
                SetMediaField(contentItem, mediaField, previous, isAttachedEditor);
                latestContentItem.Published = wasPublished;
                await mediaFileStore.TryDeleteFileAsync(mediaPath);

                var errors = string.Join("; ", result.Errors.Select(error => error.ErrorMessage));

                logger.LogWarning("AI tool '{ToolName}': Unable to attach the document to content item '{ContentItemId}' due to validation errors: {Errors}.", TheName, contentItemId, errors);

                return $"Unable to attach the document because the content item is not valid: {errors}";
            }

            var publish = isDraft.HasValue ? !isDraft.Value : wasPublished;

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}': Validation passed for content item '{ContentItemId}'. Publish: {Publish}, was published: {WasPublished}, isDraft argument: {IsDraft}.", TheName, contentItemId, publish, wasPublished, isDraft);
            }

            if (publish)
            {
                await contentManager.PublishAsync(contentItem);
            }
            else
            {
                await contentManager.SaveDraftAsync(contentItem);
            }

            // Flush the changes so other tools of the same function execution see the attached file.
            await services.GetRequiredService<ISession>().FlushAsync(cancellationToken);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed. Attached '{MediaPath}' to field '{MediaField}' of content item '{ContentItemId}', version '{VersionId}', published: {Published}, files in the field: {FileCount}.", TheName, mediaPath, mediaField, contentItemId, contentItem.ContentItemVersionId, contentItem.Published, updated.Paths.Length);
            }

            return $"The document '{document.FileName}' was attached to the '{mediaField}' field of content item '{contentItem.ContentItemId}', which was {(publish ? "published" : "saved as a draft")}. Media path: '{mediaPath}'. URL: {mediaFileStore.MapPathToPublicUrl(mediaPath)}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI tool '{ToolName}': Unable to attach the document to content item '{ContentItemId}'. Removing '{MediaPath}' from the media library.", TheName, contentItemId, mediaPath);

            // The content item did not get this file, so it must not stay behind in the media library.
            latestContentItem.Published = wasPublished;
            await mediaFileStore.TryDeleteFileAsync(mediaPath);

            throw;
        }
    }

    private static async Task<IReadOnlyList<AIDocument>> GetConversationDocumentsAsync(IServiceProvider services)
    {
        var executionContext = AIInvocationScope.Current?.ToolExecutionContext;
        var documentStore = services.GetService<IAIDocumentStore>();

        if (executionContext is null || documentStore is null)
        {
            return [];
        }

        var documents = new List<AIDocument>();

        switch (executionContext.Resource)
        {
            case ChatInteraction interaction:
                await AddDocumentsAsync(documentStore, interaction.ItemId, AIConstants.DocumentReferenceTypes.ChatInteraction, documents);
                break;

            case AIProfile profile:
                await AddDocumentsAsync(documentStore, profile.ItemId, AIConstants.DocumentReferenceTypes.Profile, documents);

                var session = AIInvocationScope.Current.ChatSession;

                if (session is null &&
                    AIInvocationScope.Current.Items.TryGetValue(nameof(AIChatSession), out var sessionObject))
                {
                    session = sessionObject as AIChatSession;
                }

                if (session is not null)
                {
                    await AddDocumentsAsync(documentStore, session.SessionId, AIConstants.DocumentReferenceTypes.ChatSession, documents);
                }

                break;
        }

        return documents;
    }

    private static async Task AddDocumentsAsync(IAIDocumentStore documentStore, string referenceId, string referenceType, List<AIDocument> documents)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            return;
        }

        foreach (var document in await documentStore.GetDocumentsAsync(referenceId, referenceType) ?? [])
        {
            if (!documents.Any(existing => string.Equals(existing.ItemId, document.ItemId, StringComparison.OrdinalIgnoreCase)))
            {
                documents.Add(document);
            }
        }
    }

    private static AIDocument SelectDocument(IReadOnlyList<AIDocument> documents, string documentId)
    {
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            return documents.FirstOrDefault(document => string.Equals(document.ItemId, documentId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return documents.Count == 1 ? documents[0] : null;
    }

    private static string FormatDocuments(IEnumerable<AIDocument> documents)
        => string.Join(", ", documents.Select(document => $"'{document.FileName}' (documentId: {document.ItemId})"));

    private static List<MediaFieldTarget> GetMediaFields(ContentTypeDefinition typeDefinition)
    {
        if (typeDefinition is null)
        {
            return [];
        }

        return typeDefinition.Parts
            .SelectMany(part => part.PartDefinition.Fields
                .Where(field => field.FieldDefinition.Name == nameof(MediaField))
                .Select(field => new MediaFieldTarget(part.Name, field)))
            .ToList();
    }

    private static MediaFieldTarget SelectMediaField(List<MediaFieldTarget> mediaFields, string partName, string fieldName)
    {
        // Models often name the field the way the candidates are listed, as "Part.Field".
        if (string.IsNullOrWhiteSpace(partName) && fieldName?.Split('.') is [var part, var field])
        {
            partName = part;
            fieldName = field;
        }

        var matches = mediaFields
            .Where(target => string.IsNullOrWhiteSpace(partName) || string.Equals(target.PartName, partName.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(target => string.IsNullOrWhiteSpace(fieldName) || string.Equals(target.FieldName, fieldName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static async Task<bool> IsAuthorizedAsync(IServiceProvider services, ILogger logger, ContentItem contentItem, string folder)
    {
        var user = services.GetService<IUserAccessor>()?.User;

        // No caller means trusted server-side code, such as a workflow or a background task.
        if (user is null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("AI tool '{ToolName}': There is no current user, so the permission checks were skipped.", TheName);
            }

            return true;
        }

        var authorizationService = services.GetRequiredService<IAuthorizationService>();

        // The same checks Orchard Core runs when a person edits the item and uploads a file to its folder.
        return await authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem)
            && await authorizationService.AuthorizeAsync(user, MediaPermissions.ManageMedia)
            && await authorizationService.AuthorizeAsync(user, MediaPermissions.ManageMediaFolder, (object)folder);
    }

    private static string GetMediaFileName(IServiceProvider services, string documentFileName)
    {
        var fileName = Path.GetFileName((documentFileName ?? string.Empty).Replace('\\', '/'));
        var normalizer = services.GetService<IMediaNameNormalizerService>();

        if (normalizer is not null)
        {
            fileName = normalizer.NormalizeFileName(fileName);
        }

        return string.IsNullOrWhiteSpace(fileName) ? "document" : fileName;
    }

    private static async Task<string> GetAvailableMediaPathAsync(IMediaFileStore mediaFileStore, string folder, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var path = mediaFileStore.Combine(folder, fileName);

        // Never overwrite a file another item or version may still point to.
        for (var suffix = 1; await mediaFileStore.GetFileInfoAsync(path) is not null; suffix++)
        {
            path = mediaFileStore.Combine(folder, $"{name}-{suffix}{extension}");
        }

        return path;
    }

    private static string[] Align(string[] values, int length)
    {
        var aligned = new string[length];

        for (var i = 0; i < length; i++)
        {
            aligned[i] = i < values.Length ? values[i] : string.Empty;
        }

        return aligned;
    }

    // The "Attached" editor keeps the original names of its files next to the paths, the same way Orchard Core's
    // MediaFieldAttachedFileNameExtensions do, which live in the Media module rather than in a package this module can reference.
    private static string[] GetAttachedFileNames(MediaField field)
    {
        if (field is null)
        {
            return [];
        }

        // An explicit cast, because the dynamic content wraps the array in a type only a conversion operator unwraps.
        var fileNames = (JsonArray)field.Content["AttachedFileNames"];

        return fileNames?.ToObject<string[]>() ?? [];
    }

    private static void SetMediaField(ContentItem contentItem, MediaFieldTarget mediaField, MediaFieldValues values, bool isAttachedEditor)
    {
        contentItem.Alter<ContentPart>(mediaField.PartName, part => part.Alter<MediaField>(mediaField.FieldName, field =>
        {
            field.Paths = values.Paths;
            field.MediaTexts = values.Texts;

            if (isAttachedEditor)
            {
                field.Content["AttachedFileNames"] = JArray.FromObject(values.AttachedFileNames);
            }
        }));
    }

    private sealed record MediaFieldValues(string[] Paths, string[] Texts, string[] AttachedFileNames);

    private sealed record MediaFieldTarget(string PartName, ContentPartFieldDefinition Definition)
    {
        public string FieldName => Definition.Name;

        public override string ToString() => $"{PartName}.{FieldName}";
    }
}
