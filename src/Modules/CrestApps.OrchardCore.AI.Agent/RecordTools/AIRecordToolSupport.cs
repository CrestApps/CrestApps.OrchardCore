using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.RecordTools;

/// <summary>
/// What the four generic record tools share: the <c>kind</c> argument and its schema, and the checks that a signed-in
/// caller may read or write the kind.
/// </summary>
internal static class AIRecordToolSupport
{
    /// <summary>
    /// The most records <c>deleteRecord</c> deletes in one call.
    /// </summary>
    public const int MaxDeleteCount = 50;

    /// <summary>
    /// Builds the JSON schema of a tool from its properties, with a <c>kind</c> property whose values are the
    /// registered kinds.
    /// </summary>
    /// <param name="options">The registered kinds.</param>
    /// <param name="properties">The other properties of the tool, in order.</param>
    /// <param name="required">The required properties, <c>kind</c> excluded.</param>
    /// <returns>The schema.</returns>
    public static JsonElement BuildSchema(AIRecordKindOptions options, JsonObject properties, params string[] required)
    {
        var allProperties = new JsonObject
        {
            ["kind"] = BuildKindSchema(options),
        };

        foreach (var property in properties)
        {
            allProperties[property.Key] = property.Value?.DeepClone();
        }

        var requiredNames = new JsonArray("kind");

        foreach (var name in required)
        {
            requiredNames.Add(name);
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = allProperties,
            ["required"] = requiredNames,
            ["additionalProperties"] = false,
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    /// <summary>
    /// Finds the kind the tool was called with, after checking that a signed-in caller holds the kind's read
    /// permission and, for a write, that the kind can be written and the caller holds its write permission. A read-only
    /// kind is refused with the name of the tool that saves or deletes its records, when its owner registered one.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="options">The registered kinds.</param>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="access">What the tool does with the records.</param>
    /// <returns>The kind, or the sentence the tool returns instead.</returns>
    public static async Task<(AIRecordKind Kind, string Refusal)> ResolveKindAsync(IServiceProvider services, AIRecordKindOptions options, AIFunctionArguments arguments, AIRecordAccess access)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(arguments);

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return (null, AIToolCallerAuthorization.NotAllowedMessage);
        }

        arguments.TryGetFirstString("kind", out var name);
        name = name?.Trim();

        if (string.IsNullOrEmpty(name) || !options.Kinds.TryGetValue(name, out var kind))
        {
            return (null, await DescribeUnknownKindAsync(services, options, name));
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, kind.EffectiveReadPermission))
        {
            return (null, AIToolCallerAuthorization.NotAllowedMessage);
        }

        if (access == AIRecordAccess.Read)
        {
            return (kind, null);
        }

        if (!kind.CanWrite)
        {
            return (null, DescribeReadOnly(kind, access));
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, kind.Permission))
        {
            return (null, AIToolCallerAuthorization.NotAllowedMessage);
        }

        return (kind, null);
    }

    /// <summary>
    /// Gets the kinds the signed-in caller may read.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="options">The registered kinds.</param>
    /// <returns>The kinds, by name.</returns>
    public static async Task<List<AIRecordKind>> GetReadableKindsAsync(IServiceProvider services, AIRecordKindOptions options)
    {
        var kinds = new List<AIRecordKind>();

        foreach (var kind in options.Kinds.Values.OrderBy(kind => kind.Name, StringComparer.Ordinal))
        {
            if (await AgentToolAuthorization.IsAuthorizedAsync(services, kind.EffectiveReadPermission))
            {
                kinds.Add(kind);
            }
        }

        return kinds;
    }

    /// <summary>
    /// Describes a kind for the <c>kind</c> argument: its description and, for a read-only kind its owner writes with
    /// dedicated tools, the names of those tools.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The description.</returns>
    public static string DescribeKind(AIRecordKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        if (kind.CanWrite || (kind.SaveToolName is null && kind.DeleteToolName is null))
        {
            return kind.Description;
        }

        var tools = kind.SaveToolName is null
            ? $"deleted with {kind.DeleteToolName}"
            : kind.DeleteToolName is null
                ? $"saved with {kind.SaveToolName}"
                : $"saved with {kind.SaveToolName} and deleted with {kind.DeleteToolName}";

        return string.IsNullOrWhiteSpace(kind.Description)
            ? $"read-only here, {tools}"
            : $"{kind.Description}; read-only here, {tools}";
    }

    /// <summary>
    /// Builds the sentence a write tool returns for a read-only kind: the tool that saves or deletes its records when its
    /// owner registered one, otherwise that a person changes them on the admin page.
    /// </summary>
    /// <param name="kind">The read-only kind.</param>
    /// <param name="access">What the tool was asked to do.</param>
    /// <returns>The sentence.</returns>
    public static string DescribeReadOnly(AIRecordKind kind, AIRecordAccess access)
    {
        ArgumentNullException.ThrowIfNull(kind);

        if (access == AIRecordAccess.Save && kind.SaveToolName is not null)
        {
            return $"Records of the kind '{kind.Name}' are not saved with saveRecord. Use {kind.SaveToolName}: its arguments carry the field names getRecord returns for this kind.";
        }

        if (access == AIRecordAccess.Delete && kind.DeleteToolName is not null)
        {
            return $"Records of the kind '{kind.Name}' are not deleted with deleteRecord. Use {kind.DeleteToolName}.";
        }

        if (access == AIRecordAccess.Delete && kind.SaveToolName is not null)
        {
            return $"Records of the kind '{kind.Name}' cannot be deleted here; a person does that on the admin page. To change one, use {kind.SaveToolName}.";
        }

        if (access == AIRecordAccess.Save && kind.DeleteToolName is not null)
        {
            return $"Records of the kind '{kind.Name}' can only be listed and read here; a person changes them on the admin page. To delete one, use {kind.DeleteToolName}.";
        }

        return $"Records of the kind '{kind.Name}' can only be listed and read here; a person changes them on the admin page.";
    }

    private static JsonObject BuildKindSchema(AIRecordKindOptions options)
    {
        var kinds = options.Kinds.Values
            .OrderBy(kind => kind.Name, StringComparer.Ordinal)
            .ToList();

        var schema = new JsonObject
        {
            ["type"] = "string",
            ["description"] = kinds.Count == 0
                ? "The kind of record. No record kinds are registered on this site."
                : "The kind of record: " + string.Join("; ", kinds.Select(kind => $"{kind.Name} ({DescribeKind(kind)})")) + ".",
        };

        if (kinds.Count > 0)
        {
            schema["enum"] = new JsonArray([.. kinds.Select(kind => (JsonNode)kind.Name)]);
        }

        return schema;
    }

    private static async Task<string> DescribeUnknownKindAsync(IServiceProvider services, AIRecordKindOptions options, string name)
    {
        var readable = await GetReadableKindsAsync(services, options);
        var start = string.IsNullOrEmpty(name)
            ? "Give the kind of record."
            : $"There is no record kind '{name}' that you can use.";

        if (readable.Count == 0)
        {
            return $"{start} You cannot use any record kind on this site.";
        }

        return $"{start} The kinds you can use are: {string.Join("; ", readable.Select(kind => $"{kind.Name} ({kind.Title})"))}.";
    }
}
