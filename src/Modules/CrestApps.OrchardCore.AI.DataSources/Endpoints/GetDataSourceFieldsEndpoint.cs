using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Endpoints;

internal static class GetDataSourceFieldsEndpoint
{
    public static IEndpointRouteBuilder AddGetDataSourceFieldsEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("ai/data-sources/fields/{indexProfileName}", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        string indexProfileName,
        IAuthorizationService authorizationService,
        IIndexProfileStore indexProfileStore,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIDataSources))
        {
            return TypedResults.Forbid();
        }

        if (string.IsNullOrEmpty(indexProfileName))
        {
            return TypedResults.BadRequest("Index profile name is required.");
        }

        var profile = await indexProfileStore.FindByNameAsync(indexProfileName);

        if (profile == null)
        {
            return TypedResults.NotFound();
        }

        var fields = GetFieldNamesFromProfile(profile);

        // Determine suggested defaults based on profile type.
        string suggestedTitleField = null;
        string suggestedContentField = null;
        string suggestedKeyField = null;

        if (string.Equals(profile.Type, IndexingConstants.ContentsIndexSource, StringComparison.OrdinalIgnoreCase))
        {
            suggestedKeyField = "ContentItemId";

            if (string.Equals(profile.ProviderName, "Elasticsearch", StringComparison.OrdinalIgnoreCase))
            {
                suggestedTitleField = "Content.ContentItem.DisplayText.keyword";
                suggestedContentField = "Content.ContentItem.FullText";
            }
            else if (string.Equals(profile.ProviderName, "AzureAISearch", StringComparison.OrdinalIgnoreCase))
            {
                suggestedTitleField = "Content__ContentItem__DisplayText__keyword";
                suggestedContentField = "Content__ContentItem__FullText";
            }
        }
        else if (string.Equals(profile.Type, AIConstants.AIDocumentsIndexingTaskType, StringComparison.OrdinalIgnoreCase))
        {
            suggestedKeyField = AIConstants.ColumnNames.ChunkId;
            suggestedTitleField = AIConstants.ColumnNames.FileName;
            suggestedContentField = AIConstants.ColumnNames.Content;
        }

        return TypedResults.Ok(new
        {
            fields = fields.Select(f => f.Name).OrderBy(n => n),
            suggestedTitleField,
            suggestedContentField,
            suggestedKeyField,
        });
    }

    private static List<FieldInfo> GetFieldNamesFromProfile(IndexProfile profile)
    {
        var fields = new List<FieldInfo>();

        if (profile.Properties == null)
        {
            return fields;
        }

        // Try to extract fields from Elasticsearch mapping.
        if (profile.Properties.TryGetPropertyValue("ElasticsearchIndexMetadata", out var esNode) && esNode != null)
        {
            var mappings = esNode["IndexMappings"]?["Mapping"]?["Properties"];
            if (mappings != null)
            {
                foreach (var prop in mappings.AsObject())
                {
                    fields.Add(new FieldInfo(prop.Key));
                }
            }
        }

        // Try to extract fields from Azure AI Search mapping.
        if (profile.Properties.TryGetPropertyValue("AzureAISearchIndexMetadata", out var azNode) && azNode != null)
        {
            var indexMappings = azNode["IndexMappings"];
            if (indexMappings is System.Text.Json.Nodes.JsonArray array)
            {
                foreach (var item in array)
                {
                    var fieldKey = item?["AzureFieldKey"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(fieldKey))
                    {
                        fields.Add(new FieldInfo(fieldKey));
                    }
                }
            }
        }

        return fields;
    }

    private sealed record FieldInfo(string Name);
}
