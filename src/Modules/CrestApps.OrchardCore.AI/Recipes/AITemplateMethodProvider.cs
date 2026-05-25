using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Scripting;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AITemplateMethodProvider : IGlobalMethodProvider
{
    private readonly ITemplateService _templateService;
    private readonly ILogger<AITemplateMethodProvider> _logger;
    private readonly GlobalMethod _globalMethod;

    public AITemplateMethodProvider(
        ITemplateService templateService,
        ILogger<AITemplateMethodProvider> logger)
    {
        _templateService = templateService;
        _logger = logger;

        _globalMethod = new GlobalMethod
        {
            Name = "renderAITemplate",
            Method = _ => (Func<string, object, object>)((templateId, arguments) =>
                ResolveTemplateContentAsync(templateId, arguments).GetAwaiter().GetResult()),
            AsyncMethod = _ => ResolveTemplateContentAsync,
        };
    }

    public IEnumerable<GlobalMethod> GetMethods()
    {
        yield return _globalMethod;
    }

    private async Task<object> ResolveTemplateContentAsync(string templateId, object arguments)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return string.Empty;
        }

        var normalizedTemplateId = templateId.Trim();
        var templateArguments = NormalizeArguments(arguments);

        if (templateArguments is { Count: > 0 })
        {
            var rendered = await _templateService.RenderAsync(normalizedTemplateId, templateArguments);

            if (string.IsNullOrWhiteSpace(rendered))
            {
                _logger.LogWarning("AI template '{TemplateId}' rendered empty content.", normalizedTemplateId);
                return string.Empty;
            }

            return rendered;
        }

        var template = await _templateService.GetAsync(normalizedTemplateId);

        if (template is null)
        {
            _logger.LogWarning("Unable to resolve AI template '{TemplateId}' by id.", normalizedTemplateId);
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(template.Content))
        {
            _logger.LogWarning("AI template '{TemplateId}' has no content.", normalizedTemplateId);
            return string.Empty;
        }

        return template.Content;
    }

    private static Dictionary<string, object> NormalizeArguments(object arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        return arguments switch
        {
            IDictionary<string, object> dictionary => new Dictionary<string, object>(dictionary, StringComparer.OrdinalIgnoreCase),
            JsonObject jsonObject => ConvertJsonObject(jsonObject),
            JsonElement jsonElement => ConvertJsonElement(jsonElement),
            JsonNode jsonNode => jsonNode is JsonObject objectNode
                ? ConvertJsonObject(objectNode)
                : throw CreateInvalidArgumentsException(),
            string json when !string.IsNullOrWhiteSpace(json) => ParseJsonArguments(json),
            _ => SerializeArguments(arguments),
        };
    }

    private static Dictionary<string, object> ParseJsonArguments(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return ConvertJsonElement(document.RootElement);
        }
        catch (JsonException ex)
        {
            throw CreateInvalidArgumentsException(ex);
        }
    }

    private static Dictionary<string, object> SerializeArguments(object arguments)
    {
        var jsonNode = JsonSerializer.SerializeToNode(arguments);

        return jsonNode as JsonObject is { } jsonObject
            ? ConvertJsonObject(jsonObject)
            : throw CreateInvalidArgumentsException();
    }

    private static Dictionary<string, object> ConvertJsonObject(JsonObject jsonObject)
    {
        var dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in jsonObject)
        {
            dictionary[property.Key] = ConvertJsonNode(property.Value);
        }

        return dictionary;
    }

    private static Dictionary<string, object> ConvertJsonElement(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind != JsonValueKind.Object)
        {
            throw CreateInvalidArgumentsException();
        }

        var dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in jsonElement.EnumerateObject())
        {
            dictionary[property.Name] = ConvertJsonElementValue(property.Value);
        }

        return dictionary;
    }

    private static object ConvertJsonNode(JsonNode jsonNode)
        => jsonNode switch
        {
            null => null,
            JsonObject jsonObject => ConvertJsonObject(jsonObject),
            JsonArray jsonArray => jsonArray.Select(ConvertJsonNode).ToArray(),
            _ => jsonNode.Deserialize<object>(),
        };

    private static object ConvertJsonElementValue(JsonElement jsonElement)
        => jsonElement.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonElement(jsonElement),
            JsonValueKind.Array => jsonElement.EnumerateArray().Select(ConvertJsonElementValue).ToArray(),
            JsonValueKind.String => jsonElement.GetString(),
            JsonValueKind.Number when jsonElement.TryGetInt64(out var int64Value) => int64Value,
            JsonValueKind.Number => jsonElement.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => jsonElement.GetRawText(),
        };

    private static ArgumentException CreateInvalidArgumentsException(Exception innerException = null)
        => innerException is null
            ? new ArgumentException("The renderAITemplate arguments must be a JSON object or a dictionary of template variables.")
            : new ArgumentException("The renderAITemplate arguments must be a JSON object or a dictionary of template variables.", innerException);
}
