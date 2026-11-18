using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.ChatSessions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.ChatInteractions;

/// <summary>
/// Lists chat interactions, as the Chat Interactions page does: the caller's own, or everyone's with the "for others"
/// permission, newest activity first. Profile Builder sessions are left out, as on that page.
/// </summary>
public sealed class ListChatInteractionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listChatInteractions";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Only interactions whose title contains this text."
        },
        "owner": {
          "type": "string",
          "description": "Only interactions of the user with this user name. Other users' interactions are only listed with the permission to list them."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many interactions per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists chat interactions with title, owner, deployment and last activity, newest first, with search and paging. Without the permission to list other users' interactions only your own are listed.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ListChatInteractions))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        arguments.TryGetFirstString("owner", out var owner);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var callerId = RunningChat.GetCallerId(services);
        var forOthers = await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ListChatInteractionsForOthers);

        IEnumerable<ChatInteraction> interactions = (await services.GetRequiredService<ICatalogManager<ChatInteraction>>().GetAllAsync(cancellationToken))
            .Where(interaction => !ChatInteractionToolSupport.IsProfileBuilderSession(interaction));

        if (!forOthers)
        {
            interactions = interactions.Where(interaction => string.Equals(interaction.OwnerId, callerId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            interactions = interactions.Where(interaction => (interaction.Title ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(owner))
        {
            var ownerName = owner.Trim();
            interactions = interactions.Where(interaction => string.Equals(interaction.Author, ownerName, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = interactions
            .OrderByDescending(interaction => interaction.ModifiedUtc ?? interaction.CreatedUtc)
            .ThenByDescending(interaction => interaction.CreatedUtc)
            .ToList();

        var localClock = services.GetRequiredService<ILocalClock>();
        var items = new List<object>();

        foreach (var interaction in ordered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(new
            {
                id = interaction.ItemId,
                title = string.IsNullOrWhiteSpace(interaction.Title) ? null : interaction.Title,
                owner = interaction.Author,
                yours = string.Equals(interaction.OwnerId, callerId, StringComparison.OrdinalIgnoreCase),
                chatDeployment = interaction.ChatDeploymentName,
                documents = interaction.Documents?.Count ?? 0,
                created = await AgentToolResults.ToLocalAsync(localClock, interaction.CreatedUtc == default ? null : interaction.CreatedUtc),
                lastActivity = await AgentToolResults.ToLocalAsync(localClock, interaction.ModifiedUtc ?? (interaction.CreatedUtc == default ? null : interaction.CreatedUtc)),
                chatPath = ChatInteractionToolSupport.GetChatPath(services, interaction.ItemId),
            });
        }

        return AgentToolResults.Json(new
        {
            items,
            total = ordered.Count,
            page,
            pageSize,
            truncated = ordered.Count > page * pageSize,
            onlyYours = !forOthers,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
        });
    }
}
