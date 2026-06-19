using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

internal static class ContentItemPayloadShapeValidator
{
    public static JsonNode AddKnownContentItemRootProperties(JsonNode referenceNode)
    {
        ArgumentNullException.ThrowIfNull(referenceNode);

        if (referenceNode is not JsonObject referenceObject)
        {
            return referenceNode;
        }

        EnsurePropertyValue(referenceObject, "ContentItemId", JsonValue.Create(string.Empty));
        EnsurePropertyValue(referenceObject, "ContentItemVersionId", JsonValue.Create(string.Empty));
        EnsurePropertyValue(referenceObject, "ContentType", JsonValue.Create(string.Empty));
        EnsurePropertyValue(referenceObject, "DisplayText", JsonValue.Create(string.Empty));
        EnsurePropertyValue(referenceObject, "Latest", JsonValue.Create(false));
        EnsurePropertyValue(referenceObject, "Published", JsonValue.Create(false));
        EnsurePropertyValue(referenceObject, "ModifiedUtc", JsonValue.Create(string.Empty));
        EnsurePropertyValue(referenceObject, "PublishedUtc", JsonValue.Create(string.Empty));
        EnsurePropertyValue(referenceObject, "CreatedUtc", JsonValue.Create(string.Empty));
        EnsurePropertyValue(referenceObject, "Owner", JsonValue.Create(string.Empty));
        EnsurePropertyValue(referenceObject, "Author", JsonValue.Create(string.Empty));

        return referenceObject;
    }

    public static IReadOnlyList<string> FindUnexpectedPaths(JsonNode inputNode, JsonNode referenceNode)
    {
        ArgumentNullException.ThrowIfNull(inputNode);
        ArgumentNullException.ThrowIfNull(referenceNode);

        var unexpectedPaths = new List<string>();
        CollectUnexpectedPaths(inputNode, referenceNode, string.Empty, unexpectedPaths);

        return unexpectedPaths;
    }

    private static void CollectUnexpectedPaths(
        JsonNode inputNode,
        JsonNode referenceNode,
        string currentPath,
        ICollection<string> unexpectedPaths)
    {
        if (inputNode is JsonObject inputObject)
        {
            if (referenceNode is not JsonObject referenceObject)
            {
                CollectLeafPaths(inputNode, currentPath, unexpectedPaths);

                return;
            }

            foreach (var property in inputObject)
            {
                var propertyPath = BuildPath(currentPath, property.Key);

                if (!TryGetProperty(referenceObject, property.Key, out var referencePropertyNode))
                {
                    CollectLeafPaths(property.Value, propertyPath, unexpectedPaths);

                    continue;
                }

                if (property.Value is null)
                {
                    if (referencePropertyNode is JsonObject or JsonArray)
                    {
                        unexpectedPaths.Add(propertyPath);
                    }

                    continue;
                }

                if (referencePropertyNode is null)
                {
                    CollectLeafPaths(property.Value, propertyPath, unexpectedPaths);

                    continue;
                }

                CollectUnexpectedPaths(property.Value, referencePropertyNode, propertyPath, unexpectedPaths);
            }

            return;
        }

        if (inputNode is JsonArray inputArray)
        {
            if (referenceNode is not JsonArray referenceArray)
            {
                CollectLeafPaths(inputNode, currentPath, unexpectedPaths);

                return;
            }

            if (referenceArray.Count == 0)
            {
                // Sample content items often leave collection-style parts such as BagPart empty.
                // Without a representative item shape, treat the array as shape-compatible here
                // and leave item-level enforcement to richer schema validation when available.
                return;
            }

            for (var i = 0; i < inputArray.Count; i++)
            {
                var itemPath = $"{currentPath}[{i}]";
                var inputItem = inputArray[i];
                var referenceItem = referenceArray[Math.Min(i, referenceArray.Count - 1)];

                if (inputItem is null)
                {
                    if (referenceItem is JsonObject or JsonArray)
                    {
                        unexpectedPaths.Add(itemPath);
                    }

                    continue;
                }

                if (referenceItem is null)
                {
                    CollectLeafPaths(inputItem, itemPath, unexpectedPaths);

                    continue;
                }

                CollectUnexpectedPaths(inputItem, referenceItem, itemPath, unexpectedPaths);
            }

            return;
        }

        if (!AreCompatibleValueKinds(inputNode, referenceNode))
        {
            unexpectedPaths.Add(currentPath);
        }
    }

    private static void CollectLeafPaths(JsonNode node, string currentPath, ICollection<string> unexpectedPaths)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject)
            {
                CollectLeafPaths(property.Value, BuildPath(currentPath, property.Key), unexpectedPaths);
            }

            if (jsonObject.Count == 0)
            {
                unexpectedPaths.Add(currentPath);
            }

            return;
        }

        if (node is JsonArray jsonArray)
        {
            for (var i = 0; i < jsonArray.Count; i++)
            {
                CollectLeafPaths(jsonArray[i], $"{currentPath}[{i}]", unexpectedPaths);
            }

            if (jsonArray.Count == 0)
            {
                unexpectedPaths.Add(currentPath);
            }

            return;
        }

        unexpectedPaths.Add(currentPath);
    }

    private static bool AreCompatibleValueKinds(JsonNode inputNode, JsonNode referenceNode)
        => inputNode switch
        {
            JsonObject => referenceNode is JsonObject,
            JsonArray => referenceNode is JsonArray,
            _ => referenceNode is not JsonObject and not JsonArray,
        };

    private static string BuildPath(string currentPath, string propertyName) =>
        string.IsNullOrEmpty(currentPath)
            ? propertyName
            : $"{currentPath}.{propertyName}";

    private static bool TryGetProperty(JsonObject jsonObject, string propertyName, out JsonNode value)
    {
        foreach (var item in jsonObject)
        {
            if (string.Equals(item.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = item.Value;

                return true;
            }
        }

        value = null;

        return false;
    }

    private static void EnsurePropertyValue(JsonObject jsonObject, string propertyName, JsonNode value)
    {
        if (!TryGetProperty(jsonObject, propertyName, out var currentValue) || currentValue is null)
        {
            jsonObject[propertyName] = value;
        }
    }
}
