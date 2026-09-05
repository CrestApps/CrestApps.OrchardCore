using System.Text.Json;

namespace CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;

/// <summary>
/// An in-memory projection of the Swagger 1.2 declarations that the Asterisk project publishes for the Asterisk REST
/// Interface. The declarations are vendored verbatim for the Asterisk release the Contact Center container image is
/// pinned to, which makes them the authoritative provider contract rather than a restatement of it.
/// </summary>
internal sealed class AriSpecification
{
    private static readonly char[] _pathSeparators = ['/'];

    private readonly Dictionary<string, AriModelDefinition> _models = new(StringComparer.Ordinal);
    private readonly List<AriOperation> _operations = [];

    /// <summary>
    /// Gets every model the loaded specification files declare, keyed by model identifier.
    /// </summary>
    public Dictionary<string, AriModelDefinition> Models => _models;

    /// <summary>
    /// Gets every HTTP operation the loaded specification files declare.
    /// </summary>
    public List<AriOperation> Operations => _operations;

    /// <summary>
    /// Loads a specification from the supplied verbatim Swagger 1.2 declaration files.
    /// </summary>
    /// <param name="specificationFilePaths">The absolute paths of the declaration files to load.</param>
    /// <returns>The parsed specification.</returns>
    public static AriSpecification Load(List<string> specificationFilePaths)
    {
        ArgumentNullException.ThrowIfNull(specificationFilePaths);

        var specification = new AriSpecification();

        foreach (var path in specificationFilePaths)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            specification.AddDeclaration(document.RootElement);
        }

        return specification;
    }

    /// <summary>
    /// Finds the operation the specification declares for the supplied method and concrete request path.
    /// </summary>
    /// <param name="httpMethod">The HTTP method of the issued request.</param>
    /// <param name="requestPath">The concrete request path, relative to the ARI base path.</param>
    /// <param name="operation">When this method returns, contains the matching operation, if one was found.</param>
    /// <returns><see langword="true"/> when the specification declares the operation; otherwise, <see langword="false"/>.</returns>
    public bool TryFindOperation(string httpMethod, string requestPath, out AriOperation operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod);
        ArgumentNullException.ThrowIfNull(requestPath);

        var requestSegments = requestPath.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var candidate in _operations)
        {
            if (!string.Equals(candidate.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var templateSegments = candidate.PathTemplate.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries);

            if (templateSegments.Length != requestSegments.Length)
            {
                continue;
            }

            var matches = true;

            for (var i = 0; i < templateSegments.Length; i++)
            {
                var templateSegment = templateSegments[i];

                if (templateSegment.StartsWith('{') && templateSegment.EndsWith('}'))
                {
                    continue;
                }

                if (!string.Equals(templateSegment, requestSegments[i], StringComparison.Ordinal))
                {
                    matches = false;

                    break;
                }
            }

            if (matches)
            {
                operation = candidate;

                return true;
            }
        }

        operation = null;

        return false;
    }

    /// <summary>
    /// Determines whether the specification declares the supplied dotted property path on the supplied model, taking
    /// model inheritance and nested model types into account.
    /// </summary>
    /// <param name="modelId">The model identifier the path is rooted at.</param>
    /// <param name="propertyPath">The dotted property path, for example <c>channel.caller.number</c>.</param>
    /// <returns><see langword="true"/> when the path resolves against the specification; otherwise, <see langword="false"/>.</returns>
    public bool DeclaresPropertyPath(string modelId, string propertyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);

        var currentModelId = modelId;
        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < segments.Length; i++)
        {
            if (currentModelId is null || !TryFindPropertyType(currentModelId, segments[i], out var propertyType))
            {
                return false;
            }

            var elementType = UnwrapCollectionType(propertyType);
            currentModelId = _models.ContainsKey(elementType)
                ? elementType
                : null;
        }

        return true;
    }

    /// <summary>
    /// Gets every property name the specification declares on the supplied model, including inherited properties.
    /// </summary>
    /// <param name="modelId">The model identifier to inspect.</param>
    /// <returns>The declared property names.</returns>
    public HashSet<string> GetDeclaredPropertyNames(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        var names = new HashSet<string>(StringComparer.Ordinal);
        var currentModelId = modelId;

        while (currentModelId is not null && _models.TryGetValue(currentModelId, out var model))
        {
            foreach (var name in model.Properties.Keys)
            {
                names.Add(name);
            }

            currentModelId = model.BaseModelId;
        }

        return names;
    }

    /// <summary>
    /// Gets the specification type declared for the supplied property on the supplied model.
    /// </summary>
    /// <param name="modelId">The model identifier to inspect.</param>
    /// <param name="propertyName">The property name to resolve.</param>
    /// <returns>The declared type, or <see langword="null"/> when the property is not declared.</returns>
    public string GetDeclaredPropertyType(string modelId, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return TryFindPropertyType(modelId, propertyName, out var propertyType)
            ? propertyType
            : null;
    }

    /// <summary>
    /// Finds every property path present in a recorded payload that the specification does not declare for the supplied
    /// model. An empty result proves the payload could genuinely have been produced by a conforming provider.
    /// </summary>
    /// <param name="modelId">The model identifier the payload claims to be an instance of.</param>
    /// <param name="payload">The recorded payload.</param>
    /// <returns>The undeclared property paths.</returns>
    public List<string> FindUndeclaredPaths(string modelId, JsonElement payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        var undeclared = new List<string>();
        CollectUndeclaredPaths(modelId, payload, string.Empty, undeclared);

        return undeclared;
    }

    private void CollectUndeclaredPaths(
        string modelId,
        JsonElement element,
        string pathPrefix,
        List<string> undeclared)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var path = string.Concat(pathPrefix, property.Name);

            if (!TryFindPropertyType(modelId, property.Name, out var propertyType))
            {
                undeclared.Add(path);

                continue;
            }

            var elementType = UnwrapCollectionType(propertyType);

            if (elementType is null || !_models.ContainsKey(elementType))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                CollectUndeclaredPaths(elementType, property.Value, string.Concat(path, "."), undeclared);
            }
            else if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    CollectUndeclaredPaths(elementType, item, string.Concat(path, "[]."), undeclared);
                }
            }
        }
    }

    private bool TryFindPropertyType(string modelId, string propertyName, out string propertyType)
    {
        var currentModelId = modelId;

        while (currentModelId is not null && _models.TryGetValue(currentModelId, out var model))
        {
            if (model.Properties.TryGetValue(propertyName, out propertyType))
            {
                return true;
            }

            currentModelId = model.BaseModelId;
        }

        propertyType = null;

        return false;
    }

    private static string UnwrapCollectionType(string propertyType)
    {
        if (propertyType is null)
        {
            return null;
        }

        if (propertyType.StartsWith("List[", StringComparison.Ordinal) && propertyType.EndsWith(']'))
        {
            return propertyType.Substring(5, propertyType.Length - 6);
        }

        return propertyType;
    }

    private void AddDeclaration(JsonElement declaration)
    {
        if (declaration.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Object)
        {
            foreach (var modelProperty in models.EnumerateObject())
            {
                AddModel(modelProperty.Name, modelProperty.Value);
            }
        }

        if (declaration.TryGetProperty("apis", out var apis) && apis.ValueKind == JsonValueKind.Array)
        {
            foreach (var api in apis.EnumerateArray())
            {
                AddApi(api);
            }
        }
    }

    private void AddModel(string modelId, JsonElement model)
    {
        if (!_models.TryGetValue(modelId, out var definition))
        {
            definition = new AriModelDefinition(modelId);
            _models[modelId] = definition;
        }

        if (model.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                var type = property.Value.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                    ? typeElement.GetString()
                    : null;
                definition.Properties[property.Name] = type;
            }
        }

        if (!model.TryGetProperty("subTypes", out var subTypes) || subTypes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var subType in subTypes.EnumerateArray())
        {
            var subTypeId = subType.GetString();

            if (string.IsNullOrWhiteSpace(subTypeId))
            {
                continue;
            }

            if (!_models.TryGetValue(subTypeId, out var subTypeDefinition))
            {
                subTypeDefinition = new AriModelDefinition(subTypeId);
                _models[subTypeId] = subTypeDefinition;
            }

            subTypeDefinition.BaseModelId = modelId;
        }
    }

    private void AddApi(JsonElement api)
    {
        if (!api.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var pathTemplate = pathElement.GetString().TrimStart('/');

        if (!api.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var operation in operations.EnumerateArray())
        {
            var httpMethod = operation.TryGetProperty("httpMethod", out var methodElement) && methodElement.ValueKind == JsonValueKind.String
                ? methodElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(httpMethod))
            {
                continue;
            }

            var nickname = operation.TryGetProperty("nickname", out var nicknameElement) && nicknameElement.ValueKind == JsonValueKind.String
                ? nicknameElement.GetString()
                : string.Empty;
            var declared = new AriOperation(pathTemplate, httpMethod, nickname);

            if (operation.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Array)
            {
                foreach (var parameter in parameters.EnumerateArray())
                {
                    var paramType = parameter.TryGetProperty("paramType", out var paramTypeElement) && paramTypeElement.ValueKind == JsonValueKind.String
                        ? paramTypeElement.GetString()
                        : null;

                    if (!string.Equals(paramType, "query", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var name = parameter.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                        ? nameElement.GetString()
                        : null;

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        declared.QueryParameterNames.Add(name);
                    }
                }
            }

            _operations.Add(declared);
        }
    }
}
