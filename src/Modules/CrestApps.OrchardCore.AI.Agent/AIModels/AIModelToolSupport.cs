using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// What the provider connection, deployment and voice tools share: the admin links, the refusals, and reading the
/// non-secret connection fields of a connection or of a deployment that holds its own connection.
/// </summary>
internal static class AIModelToolSupport
{
    /// <summary>
    /// The names of the authentication types an Azure connection takes, in the order of their stored numbers.
    /// </summary>
    public static readonly string[] AuthenticationTypes = ["Default", "ApiKey", "ManagedIdentity"];

    /// <summary>
    /// The keys under a connection's properties that hold a provider's connection fields. The legacy Azure key is
    /// still read for older connections.
    /// </summary>
    private static readonly string[] _metadataKeys =
    [
        "OpenAIConnectionMetadata",
        "AzureConnectionMetadata",
        "AzureOpenAIConnectionMetadata",
        "AzureAIInferenceConnectionMetadata",
    ];

    /// <summary>
    /// Parts of an argument name that mark a secret. No tool of this group takes one.
    /// </summary>
    private static readonly string[] _secretArgumentMarks = ["key", "secret", "password", "token", "credential"];

    /// <summary>
    /// Gets the address of a provider connection's editor.
    /// </summary>
    public static string GetConnectionEditPath(IServiceProvider services, string connectionId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "ProviderConnections", "Edit", new RouteValueDictionary { ["id"] = connectionId });

    /// <summary>
    /// Gets the address of the page that creates a provider connection.
    /// </summary>
    public static string GetConnectionCreatePath(IServiceProvider services, string providerName)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "ProviderConnections", "Create", new RouteValueDictionary { ["providerName"] = providerName });

    /// <summary>
    /// Gets the address of the provider connections page.
    /// </summary>
    public static string GetConnectionsPath(IServiceProvider services)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "ProviderConnections", "Index");

    /// <summary>
    /// Gets the address of a deployment's editor.
    /// </summary>
    public static string GetDeploymentEditPath(IServiceProvider services, string deploymentId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "Deployments", "Edit", new RouteValueDictionary { ["id"] = deploymentId });

    /// <summary>
    /// Gets the address of the page that creates a deployment.
    /// </summary>
    public static string GetDeploymentCreatePath(IServiceProvider services, string providerName)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "Deployments", "Create", new RouteValueDictionary { ["providerName"] = providerName });

    /// <summary>
    /// Gets the address of the voice library.
    /// </summary>
    public static string GetVoiceLibraryPath(IServiceProvider services, string deploymentName)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "AIVoiceLibrary", "Index", new RouteValueDictionary { ["deploymentName"] = deploymentName });

    /// <summary>
    /// The refusal for a change to a deployment or connection the running assistant talks to.
    /// </summary>
    /// <param name="what">What was to be changed, for example "the deployment 'gpt-4o'".</param>
    /// <param name="path">The admin page where a person can make the change, or <see langword="null"/>.</param>
    /// <returns>The refusal.</returns>
    public static string RunningAssistantRefusal(string what, string path)
        => $"Refused: the assistant running this chat talks to its AI model through {what}. An assistant cannot change or delete its own model or connection from a chat, because it could cut itself off. A person can make this change on the admin page{(string.IsNullOrEmpty(path) ? "." : $": {path}")}";

    /// <summary>
    /// Finds an argument that looks like a secret, such as an API key.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <returns>The name of the first such argument, or <see langword="null"/> when there is none.</returns>
    public static string FindSecretArgument(AIFunctionArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return arguments.Keys.FirstOrDefault(name =>
            _secretArgumentMarks.Any(mark => name.Contains(mark, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Reads the connection fields a person may see from a provider connection: endpoint, authentication type,
    /// identity, and whether a key is stored. The key itself is never read out.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The fields.</returns>
    public static ConnectionFields ReadConnectionFields(AIProviderConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var properties = ToJson(connection.Properties);
        var sources = new List<JsonObject>();

        foreach (var key in _metadataKeys)
        {
            if (GetCaseInsensitive(properties, key) is JsonObject metadata)
            {
                sources.Add(metadata);
            }
        }

        // A connection from configuration keeps its fields at the top of its properties.
        sources.Add(properties);

        return Read(sources);
    }

    /// <summary>
    /// Reads the connection fields a deployment that holds its own connection keeps in its properties.
    /// </summary>
    /// <param name="deployment">The deployment.</param>
    /// <returns>The fields.</returns>
    public static ConnectionFields ReadContainedConnectionFields(AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        return Read([ToJson(deployment.Properties)]);
    }

    /// <summary>
    /// Gets the features a deployment declares.
    /// </summary>
    /// <param name="deployment">The deployment.</param>
    /// <returns>The feature names.</returns>
    public static string[] GetFeatures(AIDeployment deployment)
        => deployment.TryGet<AIDeploymentMetadata>(out var metadata) && metadata.Features is { Length: > 0 }
            ? metadata.Features
            : [];

    /// <summary>
    /// Whether a deployment is the one a reference names, by technical name or ID.
    /// </summary>
    /// <param name="deployment">The deployment.</param>
    /// <param name="reference">The name or ID.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public static bool IsNamed(AIDeployment deployment, string reference)
        => !string.IsNullOrWhiteSpace(reference) &&
            (string.Equals(deployment.Name, reference.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(deployment.ItemId, reference.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether a deployment talks through a connection.
    /// </summary>
    /// <param name="deployment">The deployment.</param>
    /// <param name="connection">The connection.</param>
    /// <returns><see langword="true"/> when the deployment uses the connection.</returns>
    public static bool UsesConnection(AIDeployment deployment, AIProviderConnection connection)
        => string.Equals(deployment.ClientName, connection.Source, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(deployment.ConnectionName) &&
            (string.Equals(deployment.ConnectionName, connection.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(deployment.ConnectionName, connection.ItemId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the site's default deployment per slot, as Settings → Artificial Intelligence names them.
    /// </summary>
    /// <param name="settings">The site's default deployment settings.</param>
    /// <returns>The slot and the deployment name, for the slots that have a default.</returns>
    public static IEnumerable<(string Slot, string DeploymentName)> GetSlotDefaults(DefaultAIDeploymentSettings settings)
    {
        if (settings is null)
        {
            yield break;
        }

        (string Slot, string Name)[] defaults =
        [
            (AIDeploymentSlotNames.Chat, settings.DefaultChatDeploymentName),
            (AIDeploymentSlotNames.Utility, settings.DefaultUtilityDeploymentName),
            (AIDeploymentSlotNames.Embedding, settings.DefaultEmbeddingDeploymentName),
            (AIDeploymentSlotNames.Image, settings.DefaultImageDeploymentName),
            (AIDeploymentSlotNames.Vision, settings.DefaultVisionDeploymentName),
            (AIDeploymentSlotNames.SpeechToText, settings.DefaultSpeechToTextDeploymentName),
            (AIDeploymentSlotNames.TextToSpeech, settings.DefaultTextToSpeechDeploymentName),
            (AIDeploymentSlotNames.Realtime, settings.DefaultRealtimeDeploymentName),
        ];

        foreach (var (slot, name) in defaults)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return (slot, name.Trim());
            }
        }
    }

    /// <summary>
    /// Whether a text is an absolute web address.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns><see langword="true"/> for an absolute http or https address.</returns>
    public static bool IsWebAddress(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static ConnectionFields Read(List<JsonObject> sources)
    {
        string endpoint = null;
        string authenticationType = null;
        string identityId = null;
        var hasApiKey = false;

        foreach (var source in sources)
        {
            endpoint ??= ReadText(GetCaseInsensitive(source, "Endpoint"));
            authenticationType ??= ReadAuthenticationType(GetCaseInsensitive(source, "AuthenticationType"));
            identityId ??= ReadText(GetCaseInsensitive(source, "IdentityId"));
            hasApiKey |= !string.IsNullOrEmpty(ReadText(GetCaseInsensitive(source, "ApiKey")));
        }

        return new ConnectionFields(endpoint, authenticationType, identityId, hasApiKey);
    }

    private static JsonObject ToJson(IDictionary<string, object> properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return new JsonObject();
        }

        return JsonSerializer.SerializeToNode(properties, ExtensibleEntityExtensions.JsonSerializerOptions) as JsonObject ?? new JsonObject();
    }

    private static JsonNode GetCaseInsensitive(JsonObject json, string key)
    {
        foreach (var (name, value) in json)
        {
            if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string ReadText(JsonNode node)
        => node is JsonValue value && value.GetValueKind() == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetValue<string>())
            ? value.GetValue<string>().Trim()
            : null;

    private static string ReadAuthenticationType(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<int>(out var number))
        {
            return number >= 0 && number < AuthenticationTypes.Length ? AuthenticationTypes[number] : null;
        }

        var text = ReadText(node);

        return text is null ? null : AuthenticationTypes.FirstOrDefault(name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase)) ?? text;
    }
}

/// <summary>
/// The connection fields a person may see. The key is only reported as stored or not.
/// </summary>
/// <param name="Endpoint">The endpoint, or <see langword="null"/>.</param>
/// <param name="AuthenticationType">The authentication type of an Azure connection, or <see langword="null"/>.</param>
/// <param name="IdentityId">The managed identity, or <see langword="null"/>.</param>
/// <param name="HasApiKey">Whether a key is stored.</param>
internal sealed record ConnectionFields(string Endpoint, string AuthenticationType, string IdentityId, bool HasApiKey);
