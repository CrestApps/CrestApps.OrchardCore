using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Core.Models;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Indexing;

/// <summary>
/// Creates a search index profile, or updates the name and the content settings of one, through the index profile
/// manager the Indexes admin page uses. Everything is checked before anything is changed, so a refused call leaves the
/// index profile as it was.
/// </summary>
public sealed class SaveIndexProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveIndexProfile";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the index profile to update, from listIndexProfiles. Leave it out to create an index profile, or to update the one that has the given name."
        },
        "name": {
          "type": "string",
          "description": "The name of the index profile, unique on the site. Without id it names the index profile to update, or the one to create when none has that name. With id it is the new name."
        },
        "providerName": {
          "type": "string",
          "description": "To create: the technical name of the search provider, for example Lucene, from the sources of listIndexProfiles. It cannot be changed afterwards."
        },
        "type": {
          "type": "string",
          "description": "To create: the type of the index, for example Content, from the sources of listIndexProfiles. It cannot be changed afterwards."
        },
        "indexName": {
          "type": "string",
          "description": "To create: the name of the physical index at the provider, for example blog. Each provider has its own rules for it. It cannot be changed afterwards."
        },
        "indexedContentTypes": {
          "type": "array",
          "description": "For an index of the Content type: the technical names of the content types to index. The list replaces the stored one. At least one is needed to create the index.",
          "items": {
            "type": "string"
          }
        },
        "culture": {
          "type": "string",
          "description": "For an index of the Content type: the culture of the items to index, for example en-US, or any for every culture."
        },
        "indexLatest": {
          "type": "boolean",
          "description": "For an index of the Content type: true indexes the latest version of each item, drafts included; false indexes the published version."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates an index profile for a provider and type (for example Lucene and Content) or updates an existing one by id or name. Creating also creates the physical index and starts indexing in the background; the index name, provider and type cannot be changed afterwards. An update changes the name and, for a Content index, the indexed content types, the culture and latest or published. The analyzer and the other settings of the provider are changed on the admin page of the index.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, IndexingPermissions.ManageIndexes))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var hasId = arguments.TryGetFirstString("id", out var id);

        arguments.TryGetFirstString("name", out var name);
        name = name?.Trim();

        if (!hasId && string.IsNullOrEmpty(name))
        {
            return "Nothing was saved: give name to create an index profile, or id to update one.";
        }

        string[] contentTypes = null;

        if (arguments.TryGetFirst("indexedContentTypes", out _))
        {
            if (!arguments.TryGetFirst<string[]>("indexedContentTypes", out var givenContentTypes) || givenContentTypes is null)
            {
                return "Nothing was saved: indexedContentTypes must be a list of content type names.";
            }

            contentTypes = givenContentTypes
                .Where(contentType => !string.IsNullOrWhiteSpace(contentType))
                .Select(contentType => contentType.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var unknownContentTypes = await GetUnknownContentTypesAsync(services, contentTypes);

            if (unknownContentTypes.Count > 0)
            {
                return $"Nothing was saved: there is no content type named {string.Join(", ", unknownContentTypes.Select(contentType => $"'{contentType}'"))}. Give the technical names of content types.";
            }
        }

        arguments.TryGetFirstString("culture", out var culture);

        var changes = new ContentChanges(
            contentTypes,
            culture?.Trim(),
            arguments.TryGetFirst<bool>("indexLatest", out var indexLatest)
                ? indexLatest
                : null);

        var indexProfileManager = services.GetRequiredService<IIndexProfileManager>();
        IndexProfile indexProfile;

        if (hasId)
        {
            indexProfile = await indexProfileManager.FindByIdAsync(id.Trim());

            if (indexProfile is null)
            {
                return $"Nothing was saved: there is no index profile with the ID '{id}'. Use listIndexProfiles to get the IDs.";
            }
        }
        else
        {
            indexProfile = await indexProfileManager.FindByNameAsync(name);
        }

        if (indexProfile is null)
        {
            return await CreateAsync(arguments, indexProfileManager, name, changes);
        }

        // A name given with an ID is the new name; a name given alone only found the profile.
        var newName = hasId
            ? name
            : null;

        return await UpdateAsync(arguments, indexProfileManager, indexProfile, newName, changes);
    }

    // The steps of the Create action of the Indexes page and of the index profile recipe step: build the profile,
    // validate it, store it, create the physical index, and start indexing.
    private static async Task<object> CreateAsync(AIFunctionArguments arguments, IIndexProfileManager indexProfileManager, string name, ContentChanges changes)
    {
        var services = arguments.Services;

        if (!arguments.TryGetFirstString("providerName", out var providerName) ||
            !arguments.TryGetFirstString("type", out var type) ||
            !arguments.TryGetFirstString("indexName", out var indexName))
        {
            return $"Nothing was saved: there is no index profile named '{name}'. To create it, give providerName, type and indexName; the sources of listIndexProfiles give the providers and types.";
        }

        providerName = providerName.Trim();
        type = type.Trim();
        indexName = indexName.Trim();

        var indexingOptions = services.GetRequiredService<IOptions<IndexingOptions>>().Value;

        if (!indexingOptions.Sources.TryGetValue(new IndexProfileKey(providerName, type), out var source))
        {
            var sources = indexingOptions.Sources.Values.Select(entry => $"{entry.ProviderName} and {entry.Type}");

            return $"Nothing was saved: the site has no search provider '{providerName}' with the index type '{type}'. Available: {string.Join("; ", sources)}.";
        }

        if (await indexProfileManager.FindByNameAndProviderAsync(indexName, source.ProviderName) is not null)
        {
            return $"Nothing was saved: the provider '{source.ProviderName}' already has an index named '{indexName}'.";
        }

        var indexManager = services.GetKeyedService<IIndexManager>(source.ProviderName);

        if (indexManager is null)
        {
            return $"Nothing was saved: the provider '{source.ProviderName}' has no index manager, so its index cannot be created.";
        }

        var data = new JsonObject
        {
            [nameof(IndexProfile.Name)] = name,
            [nameof(IndexProfile.IndexName)] = indexName,
            [nameof(IndexProfile.ProviderName)] = source.ProviderName,
            [nameof(IndexProfile.Type)] = source.Type,
        };

        if (changes.ContentTypes is not null)
        {
            var contentTypes = new JsonArray();

            foreach (var contentType in changes.ContentTypes)
            {
                contentTypes.Add(contentType);
            }

            data[nameof(ContentIndexMetadata.IndexedContentTypes)] = contentTypes;
        }

        if (!string.IsNullOrEmpty(changes.Culture))
        {
            data[nameof(ContentIndexMetadata.Culture)] = changes.Culture;
        }

        if (changes.IndexLatest.HasValue)
        {
            data[nameof(ContentIndexMetadata.IndexLatest)] = changes.IndexLatest.Value;
        }

        var indexProfile = await indexProfileManager.NewAsync(source.ProviderName, source.Type, data);

        if (indexProfile is null)
        {
            return $"Nothing was saved: the provider '{source.ProviderName}' could not start an index profile of the type '{source.Type}'.";
        }

        if (changes.HasAny && !IndexProfileContent.IndexesContent(indexProfile))
        {
            return $"Nothing was saved: indexedContentTypes, culture and indexLatest are settings of a Content index, and the type '{source.Type}' is not one.";
        }

        var validation = await indexProfileManager.ValidateAsync(indexProfile);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {DescribeErrors(validation)}";
        }

        // As in the admin, the profile is stored first so every property is set when the provider creates the index.
        await indexProfileManager.CreateAsync(indexProfile);

        if (!await indexManager.CreateAsync(indexProfile))
        {
            await indexProfileManager.DeleteAsync(indexProfile);

            return $"Nothing was saved: the provider '{source.ProviderName}' could not create the index '{indexName}'. The site's log has the reason.";
        }

        await indexProfileManager.SynchronizeAsync(indexProfile);

        var localClock = services.GetRequiredService<ILocalClock>();

        return AgentToolResults.Json(new
        {
            saved = true,
            created = true,
            indexProfile = await IndexProfileContent.DescribeAsync(indexProfile, localClock),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            note = "The index profile and its physical index were created. Indexing was started in the background and is not finished yet: search results are incomplete until it is.",
        });
    }

    // The steps of the Edit action of the Indexes page: change the profile, validate it, store it. The changes are
    // validated on a copy first, so a refused change never stays on the profile the rest of the request reads.
    private static async Task<object> UpdateAsync(AIFunctionArguments arguments, IIndexProfileManager indexProfileManager, IndexProfile indexProfile, string newName, ContentChanges changes)
    {
        var fixedSettings = new (string Argument, string Stored)[]
        {
            ("providerName", indexProfile.ProviderName),
            ("type", indexProfile.Type),
            ("indexName", indexProfile.IndexName),
        };

        foreach (var (argument, stored) in fixedSettings)
        {
            if (arguments.TryGetFirstString(argument, out var given) && !string.Equals(given.Trim(), stored, StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: the {argument} of the index profile '{indexProfile.Name}' is '{stored}' and cannot be changed. Create another index profile, or leave {argument} out.";
            }
        }

        if (string.Equals(newName, indexProfile.Name, StringComparison.Ordinal))
        {
            newName = null;
        }

        if (changes.HasAny && !IndexProfileContent.IndexesContent(indexProfile))
        {
            return $"Nothing was saved: indexedContentTypes, culture and indexLatest are settings of a Content index, and the type of '{indexProfile.Name}' is '{indexProfile.Type}'.";
        }

        if (newName is null && !changes.HasAny)
        {
            return $"Nothing was saved: give a new name (with id), indexedContentTypes, culture or indexLatest to change the index profile '{indexProfile.Name}'.";
        }

        var draft = new IndexProfile
        {
            Id = indexProfile.Id,
            ProviderName = indexProfile.ProviderName,
            Type = indexProfile.Type,
            Name = indexProfile.Name,
            IndexName = indexProfile.IndexName,
            IndexFullName = indexProfile.IndexFullName,
            CreatedUtc = indexProfile.CreatedUtc,
            Author = indexProfile.Author,
            OwnerId = indexProfile.OwnerId,
            Properties = indexProfile.Properties?.DeepClone().AsObject() ?? [],
        };

        Apply(draft, newName, changes);

        var validation = await indexProfileManager.ValidateAsync(draft);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {DescribeErrors(validation)}";
        }

        Apply(indexProfile, newName, changes);

        await indexProfileManager.UpdateAsync(indexProfile);

        var localClock = arguments.Services.GetRequiredService<ILocalClock>();

        return AgentToolResults.Json(new
        {
            saved = true,
            created = false,
            indexProfile = await IndexProfileContent.DescribeAsync(indexProfile, localClock),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            note = changes.HasAny
                ? "The settings were saved. Items that are already indexed are not indexed again: use synchronizeIndexProfile with reset to index the existing items with the new settings."
                : null,
        });
    }

    private static void Apply(IndexProfile indexProfile, string newName, ContentChanges changes)
    {
        if (newName is not null)
        {
            indexProfile.Name = newName;
        }

        if (!changes.HasAny)
        {
            return;
        }

        indexProfile.Alter<ContentIndexMetadata>(metadata =>
        {
            if (changes.ContentTypes is not null)
            {
                metadata.IndexedContentTypes = changes.ContentTypes;
            }

            if (!string.IsNullOrEmpty(changes.Culture))
            {
                metadata.Culture = changes.Culture;
            }

            if (changes.IndexLatest.HasValue)
            {
                metadata.IndexLatest = changes.IndexLatest.Value;
            }
        });
    }

    private static async Task<List<string>> GetUnknownContentTypesAsync(IServiceProvider services, string[] contentTypes)
    {
        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var unknown = new List<string>();

        foreach (var contentType in contentTypes)
        {
            if (await contentDefinitionManager.GetTypeDefinitionAsync(contentType) is null)
            {
                unknown.Add(contentType);
            }
        }

        return unknown;
    }

    private static string DescribeErrors(ValidationResultDetails validation)
        => string.Join(" ", validation.Errors.Select(error => error.ErrorMessage));

    private sealed record ContentChanges(string[] ContentTypes, string Culture, bool? IndexLatest)
    {
        public bool HasAny
            => ContentTypes is not null || !string.IsNullOrEmpty(Culture) || IndexLatest.HasValue;
    }
}
