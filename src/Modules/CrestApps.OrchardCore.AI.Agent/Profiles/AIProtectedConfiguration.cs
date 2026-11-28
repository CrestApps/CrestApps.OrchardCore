using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// What the assistant running the current AI turn is built from: its profile, the session's profile, the admin chat
/// widget's profile, the agents those can call, and the persona, prompt templates, tool instances, MCP and A2A
/// connections and AI data sources all of them use. None of it may be changed or deleted from inside the turn.
/// </summary>
internal sealed class AIProtectedConfiguration
{
    /// <summary>
    /// The prefix the template service gives a stored system prompt template's ID and name.
    /// </summary>
    private const string TemplateAliasPrefix = "ai-profile:";

    private readonly bool _everything;

    private AIProtectedConfiguration(bool everything)
    {
        _everything = everything;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProtectedConfiguration"/> class that protects nothing yet.
    /// </summary>
    public AIProtectedConfiguration()
    {
    }

    /// <summary>
    /// Gets the configuration outside an AI turn: nothing is protected.
    /// </summary>
    public static AIProtectedConfiguration None { get; } = new(everything: false);

    /// <summary>
    /// Gets the configuration used when the running assistant cannot be worked out: everything is protected.
    /// </summary>
    public static AIProtectedConfiguration Everything { get; } = new(everything: true);

    /// <summary>
    /// Gets the IDs of the protected profiles.
    /// </summary>
    public HashSet<string> ProfileIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the technical names of the protected profiles.
    /// </summary>
    public HashSet<string> ProfileNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the names of the personas the protected profiles link to.
    /// </summary>
    public HashSet<string> PersonaNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the IDs and names of the system prompt templates, speaking styles and guardrail topics the protected
    /// profiles select.
    /// </summary>
    public HashSet<string> TemplateReferences { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the names of the tool instances the protected profiles use.
    /// </summary>
    public HashSet<string> ToolInstanceNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the IDs of the MCP connections the protected profiles and a running chat interaction use.
    /// </summary>
    public HashSet<string> McpConnectionIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the IDs of the A2A connections the protected profiles and a running chat interaction use.
    /// </summary>
    public HashSet<string> A2AConnectionIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the IDs of the AI data sources the protected profiles, a running chat interaction and the protected tool
    /// instances search.
    /// </summary>
    public HashSet<string> DataSourceIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether an AI data source is protected.
    /// </summary>
    /// <param name="dataSourceId">The ID of the data source, which is what profiles and tool instances use.</param>
    /// <returns><see langword="true"/> when it may not be changed or deleted from inside the AI turn.</returns>
    public bool IsProtectedDataSource(string dataSourceId)
        => _everything || (!string.IsNullOrEmpty(dataSourceId) && DataSourceIds.Contains(dataSourceId));

    /// <summary>
    /// Whether a profile is protected.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns><see langword="true"/> when it may not be changed or deleted from inside the AI turn.</returns>
    public bool IsProtected(AIProfile profile)
        => profile is not null && (_everything ||
            (!string.IsNullOrEmpty(profile.ItemId) && ProfileIds.Contains(profile.ItemId)) ||
            (!string.IsNullOrEmpty(profile.Name) && ProfileNames.Contains(profile.Name)));

    /// <summary>
    /// Whether a persona is protected.
    /// </summary>
    /// <param name="personaName">The technical name of the persona, which is what profiles link to.</param>
    /// <returns><see langword="true"/> when it may not be changed or deleted from inside the AI turn.</returns>
    public bool IsProtectedPersona(string personaName)
        => _everything || (!string.IsNullOrEmpty(personaName) && PersonaNames.Contains(personaName));

    /// <summary>
    /// Whether a stored template is protected.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns><see langword="true"/> when it may not be changed or deleted from inside the AI turn.</returns>
    public bool IsProtectedTemplate(AIProfileTemplate template)
        => template is not null && (_everything ||
            (!string.IsNullOrEmpty(template.Name) && (TemplateReferences.Contains(template.Name) || TemplateReferences.Contains(TemplateAliasPrefix + template.Name))) ||
            (!string.IsNullOrEmpty(template.ItemId) && (TemplateReferences.Contains(template.ItemId) || TemplateReferences.Contains(TemplateAliasPrefix + template.ItemId))));

    /// <summary>
    /// Whether a tool instance is protected.
    /// </summary>
    /// <param name="instanceName">The name of the tool instance, which is what profiles use.</param>
    /// <returns><see langword="true"/> when it may not be changed or deleted from inside the AI turn.</returns>
    public bool IsProtectedToolInstance(string instanceName)
        => _everything || (!string.IsNullOrEmpty(instanceName) && ToolInstanceNames.Contains(instanceName));

    /// <summary>
    /// Whether an MCP connection is protected.
    /// </summary>
    /// <param name="connectionId">The ID of the connection, which is what profiles use.</param>
    /// <returns><see langword="true"/> when it may not be changed or deleted from inside the AI turn.</returns>
    public bool IsProtectedMcpConnection(string connectionId)
        => _everything || (!string.IsNullOrEmpty(connectionId) && McpConnectionIds.Contains(connectionId));

    /// <summary>
    /// Whether an A2A connection is protected.
    /// </summary>
    /// <param name="connectionId">The ID of the connection, which is what profiles use.</param>
    /// <returns><see langword="true"/> when it may not be changed or deleted from inside the AI turn.</returns>
    public bool IsProtectedA2AConnection(string connectionId)
        => _everything || (!string.IsNullOrEmpty(connectionId) && A2AConnectionIds.Contains(connectionId));

    /// <summary>
    /// Adds a profile to the protected set.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns><see langword="true"/> when it was not in the set yet.</returns>
    internal bool AddProfile(AIProfile profile)
    {
        var added = false;

        if (!string.IsNullOrEmpty(profile.ItemId))
        {
            added |= ProfileIds.Add(profile.ItemId);
        }

        if (!string.IsNullOrEmpty(profile.Name))
        {
            added |= ProfileNames.Add(profile.Name);
        }

        return added;
    }

    /// <summary>
    /// Adds what a profile, or another entity a turn can run on, builds its prompt and tools from.
    /// </summary>
    /// <param name="entity">The profile or the chat interaction.</param>
    internal void AddFrom(ExtensibleEntity entity)
    {
        if (entity.TryGet<AIPersonaMetadata>(out var persona) && !string.IsNullOrWhiteSpace(persona.PersonaId))
        {
            PersonaNames.Add(persona.PersonaId.Trim());
        }

        if (entity.TryGet<PromptTemplateMetadata>(out var prompts))
        {
            AddAll(TemplateReferences, prompts.Templates?.Select(template => template.TemplateId));
        }

        if (entity.TryGet<AIToolInstanceMetadata>(out var instances))
        {
            AddAll(ToolInstanceNames, instances.ToolInstanceNames);
        }

        if (entity.TryGet<DataSourceMetadata>(out var dataSource) && !string.IsNullOrWhiteSpace(dataSource.DataSourceId))
        {
            DataSourceIds.Add(dataSource.DataSourceId.Trim());
        }

        if (entity is AIProfile profile)
        {
            if (profile.TryGetSettings<AIProfileSpeakingStyleSettings>(out var styles))
            {
                AddAll(TemplateReferences, styles.Styles);
            }

            if (profile.TryGetSettings<AIProfileGuardrailSettings>(out var guardrails))
            {
                AddAll(TemplateReferences, guardrails.Topics);
            }

            var capabilities = AIProfileCapabilities.Read(profile);

            McpConnectionIds.UnionWith(capabilities.McpConnectionIds);
            A2AConnectionIds.UnionWith(capabilities.A2AConnectionIds);
        }
        else if (entity is ChatInteraction interaction)
        {
            AddAll(McpConnectionIds, interaction.McpConnectionIds);
            AddAll(A2AConnectionIds, interaction.A2AConnectionIds);
        }
    }

    private static void AddAll(HashSet<string> set, IEnumerable<string> values)
    {
        if (values is null)
        {
            return;
        }

        set.UnionWith(values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
    }
}
