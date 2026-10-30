using System.Globalization;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// The base of the copilot's tools: a name, a description, a JSON schema and loose argument checking.
/// </summary>
public abstract class AICopilotTool : AIFunction
{
    private readonly JsonElement _jsonSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICopilotTool"/> class.
    /// </summary>
    /// <param name="properties">The JSON of the schema's properties.</param>
    protected AICopilotTool(string properties)
    {
        _jsonSchema = JsonSerializer.Deserialize<JsonElement>($$"""{"type":"object","properties":{{{properties}}},"additionalProperties":false}""");
    }

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object> { ["Strict"] = false };

    /// <summary>
    /// Reads a date argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The date in UTC, or <see langword="null"/>.</returns>
    protected static DateTime? ReadDate(AIFunctionArguments arguments, string name)
        => arguments.TryGetFirstString(name, out var value) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="fallback">The value when it is missing.</param>
    /// <param name="max">The largest value.</param>
    /// <returns>The number.</returns>
    protected static int ReadInt(AIFunctionArguments arguments, string name, int fallback, int max)
        => arguments.TryGetFirstString(name, out var value) && int.TryParse(value, out var number) ? Math.Clamp(number, 1, max) : fallback;
}

/// <summary>
/// Lists AI conversations, newest activity first, so the copilot can look at what happened.
/// </summary>
public sealed class ListAIConversationsTool : AICopilotTool
{
    public const string TheName = "listAIConversations";

    public ListAIConversationsTool()
        : base("""
            "profileId": { "type": "string", "description": "Only this AI profile's conversations." },
            "status": { "type": "string", "enum": ["Active", "Closed", "Abandoned"], "description": "Only conversations in this status." },
            "sinceUtc": { "type": "string", "description": "Only conversations with activity at or after this ISO 8601 time." },
            "untilUtc": { "type": "string", "description": "Only conversations with activity before this ISO 8601 time." },
            "take": { "type": "integer", "description": "How many to list, at most 50. Defaults to 20." }
            """)
    {
    }

    public override string Name => TheName;

    public override string Description => "Lists AI conversations (chats, calls and texts), newest activity first, with their title, status and times. Use viewAIConversation to read one.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var session = arguments.Services.GetRequiredService<ISession>();
        var options = arguments.Services.GetRequiredService<IOptions<YesSqlStoreOptions>>().Value;
        var query = session.Query<AIChatSession, AIChatSessionIndex>(options.AICollectionName);

        if (arguments.TryGetFirstString("profileId", out var profileId) && !string.IsNullOrWhiteSpace(profileId))
        {
            query = query.Where(index => index.ProfileId == profileId);
        }

        if (arguments.TryGetFirstString("status", out var statusText) && Enum.TryParse<ChatSessionStatus>(statusText, true, out var status))
        {
            query = query.Where(index => index.Status == status);
        }

        if (ReadDate(arguments, "sinceUtc") is { } since)
        {
            query = query.Where(index => index.LastActivityUtc >= since);
        }

        if (ReadDate(arguments, "untilUtc") is { } until)
        {
            query = query.Where(index => index.LastActivityUtc < until);
        }

        var conversations = await query.OrderByDescending(index => index.LastActivityUtc).Take(ReadInt(arguments, "take", 20, 50)).ListAsync(cancellationToken);

        return JsonSerializer.Serialize(conversations.Select(conversation => new
        {
            conversation.SessionId,
            conversation.ProfileId,
            conversation.Title,
            Status = conversation.Status.ToString(),
            conversation.CreatedUtc,
            conversation.LastActivityUtc,
        }));
    }
}

/// <summary>
/// Reads one AI conversation: what was said, its variables, extracted fields, analysis, evaluations and flow.
/// </summary>
public sealed class ViewAIConversationTool : AICopilotTool
{
    public const string TheName = "viewAIConversation";

    public ViewAIConversationTool()
        : base("""
            "sessionId": { "type": "string", "description": "The conversation's session id." },
            "maxTurns": { "type": "integer", "description": "How many of the last turns to include, at most 200. Defaults to 60." }
            """)
    {
    }

    public override string Name => TheName;

    public override string Description => "Reads an AI conversation: its transcript, conversation variables, extracted fields, post-session analysis results, QA evaluations with their issues, and the conversation flow stages it went through.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetFirstString("sessionId", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return "Give the conversation's sessionId.";
        }

        var services = arguments.Services;
        var conversation = await services.GetRequiredService<IAIChatSessionManager>().FindByIdAsync(sessionId, cancellationToken);

        if (conversation is null)
        {
            return $"No conversation has the session id '{sessionId}'.";
        }

        var turns = AIConversationTranscript.GetTurns(await services.GetRequiredService<IAIChatSessionPromptStore>().GetPromptsAsync(sessionId));
        var evaluations = services.GetService<IAIConversationEvaluationStore>() is { } evaluationStore
            ? await evaluationStore.ListBySessionAsync(sessionId, cancellationToken)
            : [];

        return JsonSerializer.Serialize(new
        {
            conversation.SessionId,
            conversation.ProfileId,
            conversation.Title,
            Status = conversation.Status.ToString(),
            conversation.CreatedUtc,
            conversation.LastActivityUtc,
            Transcript = turns.TakeLast(ReadInt(arguments, "maxTurns", 60, 200)).Select(turn => new { turn.Role, turn.Text }),
            Variables = conversation.GetConversationVariables(),
            ExtractedFields = conversation.ExtractedData.ToDictionary(field => field.Key, field => field.Value?.Values ?? []),
            Results = conversation.PostSessionResults.ToDictionary(result => result.Key, result => result.Value?.Value),
            Evaluations = evaluations,
            Flow = conversation.TryGet<AIConversationFlowState>(out var flow) ? flow : null,
        });
    }
}

/// <summary>
/// Reads what the AI remembers about a contact.
/// </summary>
public sealed class GetAIContactMemoryTool : AICopilotTool
{
    public const string TheName = "getAIContactMemory";

    public GetAIContactMemoryTool()
        : base("""
            "contactId": { "type": "string", "description": "The contact's content item id." }
            """)
    {
    }

    public override string Name => TheName;

    public override string Description => "Reads the memory note the AI keeps about a contact across conversations and channels.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetFirstString("contactId", out var contactId) || string.IsNullOrWhiteSpace(contactId))
        {
            return "Give the contactId.";
        }

        var memory = await arguments.Services.GetRequiredService<AIContactMemoryService>().GetAsync(contactId, cancellationToken);

        return string.IsNullOrWhiteSpace(memory) ? "The AI remembers nothing about this contact yet." : memory;
    }
}
