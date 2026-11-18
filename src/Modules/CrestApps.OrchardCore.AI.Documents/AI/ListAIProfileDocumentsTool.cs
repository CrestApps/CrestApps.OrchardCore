using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.AI;

/// <summary>
/// Lists the documents an AI profile answers from, as the profile's Knowledge tab and the profile documents API do.
/// </summary>
public sealed class ListAIProfileDocumentsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIProfileDocuments";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "profileId": {
          "type": "string",
          "description": "The ID of the AI profile. Give this or profileName."
        },
        "profileName": {
          "type": "string",
          "description": "The technical name of the AI profile. Give this or profileId."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many documents a page holds, at most 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the documents an AI profile answers from, with file name, type and size, and the profile's document retrieval settings. Use it to see what knowledge a profile has before adding or removing a document.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AIModuleTools.IsAuthorizedAsync(services, AIPermissions.ManageAIProfiles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var profileId = AIModuleTools.ReadString(arguments, "profileId");
        var profileName = AIModuleTools.ReadString(arguments, "profileName");

        if (profileId is null && profileName is null)
        {
            return "Give the profile's ID in profileId or its technical name in profileName.";
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profile = profileId is not null
            ? await profileManager.FindByIdAsync(profileId, cancellationToken)
            : await profileManager.FindByNameAsync(profileName, cancellationToken);

        if (profile is null)
        {
            return $"No AI profile has the {(profileId is not null ? $"ID '{profileId}'" : $"name '{profileName}'")}. Call listAIProfiles to find it.";
        }

        var metadata = profile.TryGet<DocumentsMetadata>(out var stored) ? stored : new DocumentsMetadata();
        var documents = metadata.Documents ?? [];
        var (page, pageSize) = AIModuleTools.ReadPaging(arguments);
        var siteService = services.GetService<ISiteService>();
        var settings = siteService is null ? null : await siteService.GetSettingsAsync<InteractionDocumentSettings>();

        return AIModuleTools.Json(new
        {
            profile = new
            {
                id = profile.ItemId,
                name = profile.Name,
                displayText = profile.DisplayText,
            },
            items = documents
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(document => new
                {
                    id = document.DocumentId,
                    fileName = document.FileName,
                    contentType = document.ContentType,
                    size = document.FileSize,
                }),
            total = documents.Count,
            page,
            pageSize,
            truncated = page * pageSize < documents.Count,
            retrieval = new
            {
                topN = metadata.DocumentTopN ?? 3,
                retrievalMode = metadata.RetrievalMode?.ToString(),
                maxIndexableCharacters = metadata.MaxIndexableCharacters,
                describeFiguresInUploads = metadata.DescribeFiguresInUploads,
            },
            documentsIndex = string.IsNullOrEmpty(settings?.IndexProfileName)
                ? "No index for AI documents is set in the AI settings, so the documents cannot be searched."
                : settings.IndexProfileName,
        });
    }
}
