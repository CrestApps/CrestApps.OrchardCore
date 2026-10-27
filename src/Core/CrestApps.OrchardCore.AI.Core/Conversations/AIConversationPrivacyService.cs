using System.Text;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Options of <see cref="AIConversationPrivacyService"/>.
/// </summary>
public sealed class AIConversationPrivacyOptions
{
    /// <summary>
    /// Gets or sets whether the Chat Analytics extracted data records are redacted and deleted with the
    /// conversation. Set by the Chat Analytics feature, which creates their table; the store is registered without it.
    /// </summary>
    public bool IncludeExtractedDataRecords { get; set; }
}

/// <summary>
/// Applies an AI profile's storage mode to a conversation that has ended, and deletes conversations past their
/// retention.
/// </summary>
public sealed class AIConversationPrivacyService
{
    private const int MaxTextForFinding = 24_000;

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    private readonly IAIChatSessionStore _sessionStore;
    private readonly IEnumerable<IAIChatSessionHandler> _sessionHandlers;
    private readonly ISession _session;
    private readonly string _collectionName;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIConversationEvaluationStore _evaluationStore;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly AIConversationPrivacyOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationPrivacyService"/> class.
    /// </summary>
    /// <param name="sessionStore">The chat session store, which saves without touching the last activity.</param>
    /// <param name="sessionHandlers">The chat session handlers told of a deletion, which remove the session's documents.</param>
    /// <param name="session">The document session.</param>
    /// <param name="storeOptions">The store options that name the AI collection.</param>
    /// <param name="promptStore">The prompt store.</param>
    /// <param name="evaluationStore">The evaluation store.</param>
    /// <param name="deploymentManager">The deployment manager the utility model is resolved from.</param>
    /// <param name="clientFactory">The AI client factory.</param>
    /// <param name="serviceProvider">The service provider the optional extracted data store is read from.</param>
    /// <param name="options">The options.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger a failing session handler is written to.</param>
    public AIConversationPrivacyService(
        IAIChatSessionStore sessionStore,
        IEnumerable<IAIChatSessionHandler> sessionHandlers,
        ISession session,
        IOptions<YesSqlStoreOptions> storeOptions,
        IAIChatSessionPromptStore promptStore,
        IAIConversationEvaluationStore evaluationStore,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory,
        IServiceProvider serviceProvider,
        IOptions<AIConversationPrivacyOptions> options,
        IClock clock,
        ILogger<AIConversationPrivacyService> logger)
    {
        _sessionStore = sessionStore;
        _sessionHandlers = sessionHandlers;
        _session = session;
        _collectionName = storeOptions.Value.AICollectionName;
        _promptStore = promptStore;
        _evaluationStore = evaluationStore;
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Applies a storage mode to a conversation. A conversation it was applied to already is left alone.
    /// </summary>
    /// <param name="session">The conversation.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether anything was changed.</returns>
    /// <exception cref="Exception">The utility model could not be asked for the personal details; nothing is changed.</exception>
    public async Task<bool> ApplyAsync(AIChatSession session, AIDataStorageMode mode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (mode == AIDataStorageMode.Everything || session.TryGet<AIConversationPrivacyMetadata>(out _))
        {
            return false;
        }

        var prompts = await _promptStore.GetPromptsAsync(session.SessionId);
        var evaluations = await _evaluationStore.ListBySessionAsync(session.SessionId, cancellationToken);
        var extractedData = await FindExtractedDataAsync(session, cancellationToken);

        if (mode == AIDataStorageMode.MetadataOnly)
        {
            await _promptStore.DeleteAllPromptsAsync(session.SessionId);
            Strip(session, evaluations, extractedData);
        }
        else
        {
            // Everything is read first and the details found in all of it at once, so a name the customer gave in
            // the first turn is also taken out of the summary written from it.
            var terms = await FindPersonalDataAsync(CollectText(session, prompts, evaluations, extractedData), cancellationToken);

            foreach (var prompt in prompts)
            {
                var redacted = AIPersonalDataRedactor.Redact(prompt.Content, terms);

                if (!string.Equals(redacted, prompt.Content, StringComparison.Ordinal))
                {
                    prompt.Content = redacted;
                    await _promptStore.UpdateAsync(prompt, cancellationToken);
                }
            }

            Redact(session, evaluations, extractedData, terms);
        }

        session.RemoteAddress = null;
        session.Put(new AIConversationPrivacyMetadata { Mode = mode, AppliedUtc = _clock.UtcNow });
        await _sessionStore.SaveAsync(session, cancellationToken);

        foreach (var evaluation in evaluations)
        {
            await _evaluationStore.SaveAsync(evaluation, cancellationToken);
        }

        if (extractedData is not null)
        {
            await GetExtractedDataStore().SaveAsync(extractedData, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Deletes a conversation with its transcript, documents, extracted data and evaluations.
    /// </summary>
    /// <param name="sessionId">The conversation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the conversation existed.</returns>
    public async Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        // The session manager deletes only the signed-in user's own sessions, so the session is found and deleted
        // here, as the manager does it, for a conversation of any user, a visitor or a phone call.
        var chatSession = await _sessionStore.FindByIdAsync(sessionId, cancellationToken);

        if (chatSession is null)
        {
            return false;
        }

        await _sessionHandlers.InvokeAsync((handler, context) => handler.DeletingAsync(context), new DeletingContext<AIChatSession>(chatSession), _logger);

        await _evaluationStore.DeleteBySessionAsync(sessionId, cancellationToken);

        var extractedDataStore = GetExtractedDataStore();

        if (extractedDataStore is not null)
        {
            await extractedDataStore.DeleteAsync(sessionId, cancellationToken);
        }

        await _promptStore.DeleteAllPromptsAsync(sessionId);
        _session.Delete(chatSession, _collectionName);

        await _sessionHandlers.InvokeAsync((handler, context) => handler.DeletedAsync(context), new DeletedContext<AIChatSession>(chatSession), _logger);

        return true;
    }

    /// <summary>
    /// Asks the utility model for the names, street addresses, dates of birth and identity numbers in a text, which
    /// patterns can't recognize. Without a utility deployment only the patterns are used.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<AIPersonalDataTerm>> FindPersonalDataAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var deployment = await _deploymentManager.ResolveUtilityOrDefaultAsync();

        if (deployment is null)
        {
            return [];
        }

        var client = await _clientFactory.CreateChatClientAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: AIUsageFeaturePurposes.PersonalDataRedaction));

        var response = await client.GetResponseAsync<AIPersonalDataFindings>(
            [
                new ChatMessage(ChatRole.System, FindingPrompt),
                new ChatMessage(ChatRole.User, text.Length > MaxTextForFinding ? text[..MaxTextForFinding] : text),
            ],
            _jsonOptions,
            cancellationToken: cancellationToken);

        return WithNameParts(response.Result?.Items ?? []);
    }

    /// <summary>
    /// Adds each part of a found full name as a name of its own, because a conversation that learns "Marcus Bell"
    /// goes on to say "Marcus".
    /// </summary>
    /// <param name="terms">The details found.</param>
    internal static IReadOnlyList<AIPersonalDataTerm> WithNameParts(IEnumerable<AIPersonalDataTerm> terms)
    {
        var found = terms.Where(term => !string.IsNullOrWhiteSpace(term?.Text)).ToList();
        var parts = found
            .Where(term => string.Equals(term.Kind?.Trim(), "name", StringComparison.OrdinalIgnoreCase))
            .SelectMany(term => term.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(part => part.Trim(',', '.', '\'', '"'))
            .Where(part => part.Length >= 3 && char.IsUpper(part[0]))
            .Where(part => !found.Any(term => string.Equals(term.Text.Trim(), part, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(part => new AIPersonalDataTerm { Text = part, Kind = "name" });

        return [.. found, .. parts];
    }

    internal const string FindingPrompt =
        """
        You find personal details in a conversation between a customer and a business so they can be redacted.
        List every place the text names a private person (each full name, and each first or last name used on its own), a
        street address or part of one, a date of birth, or an account, member, policy, driver's license, passport or
        other identity number. Do not list the business, its products, its staff's first names, the assistant's own
        name, cities or countries on their own, order numbers, prices or dates that are not a date of birth.
        Copy each detail exactly as it appears in the text.
        Answer only with JSON: {"items": [{"text": "", "kind": "name|address|birthdate|id"}]}.
        """;

    internal static string CollectText(AIChatSession session, IEnumerable<AIChatSessionPrompt> prompts, IEnumerable<AIConversationEvaluation> evaluations, AIChatSessionExtractedDataRecord extractedData)
    {
        var builder = new StringBuilder();
        Append(builder, session.Title);

        foreach (var prompt in prompts)
        {
            Append(builder, prompt.Content);
        }

        foreach (var result in session.PostSessionResults.Values)
        {
            Append(builder, result?.Value);
        }

        foreach (var field in session.ExtractedData.Values)
        {
            foreach (var value in field?.Values ?? [])
            {
                Append(builder, value);
            }
        }

        foreach (var evaluation in evaluations)
        {
            Append(builder, evaluation.Summary);

            foreach (var issue in evaluation.Issues)
            {
                Append(builder, issue.Quote);
            }
        }

        foreach (var values in extractedData?.Values.Values ?? Enumerable.Empty<List<string>>())
        {
            foreach (var value in values)
            {
                Append(builder, value);
            }
        }

        return builder.ToString();
    }

    internal static void Redact(AIChatSession session, IEnumerable<AIConversationEvaluation> evaluations, AIChatSessionExtractedDataRecord extractedData, IReadOnlyList<AIPersonalDataTerm> terms)
    {
        session.Title = AIPersonalDataRedactor.Redact(session.Title, terms);

        foreach (var result in session.PostSessionResults.Values.Where(result => result is not null))
        {
            result.Value = AIPersonalDataRedactor.Redact(result.Value, terms);
        }

        foreach (var field in session.ExtractedData.Values.Where(field => field?.Values is not null))
        {
            field.Values = field.Values.Select(value => AIPersonalDataRedactor.Redact(value, terms)).ToList();
        }

        foreach (var evaluation in evaluations)
        {
            evaluation.Summary = AIPersonalDataRedactor.Redact(evaluation.Summary, terms);

            foreach (var criterion in evaluation.Criteria)
            {
                criterion.Reason = AIPersonalDataRedactor.Redact(criterion.Reason, terms);
            }

            foreach (var issue in evaluation.Issues)
            {
                issue.Detail = AIPersonalDataRedactor.Redact(issue.Detail, terms);
                issue.Quote = AIPersonalDataRedactor.Redact(issue.Quote, terms);
            }
        }

        if (extractedData is not null)
        {
            foreach (var key in extractedData.Values.Keys.ToList())
            {
                extractedData.Values[key] = extractedData.Values[key].Select(value => AIPersonalDataRedactor.Redact(value, terms)).ToList();
            }
        }
    }

    internal static void Strip(AIChatSession session, IEnumerable<AIConversationEvaluation> evaluations, AIChatSessionExtractedDataRecord extractedData)
    {
        session.Title = null;

        foreach (var result in session.PostSessionResults.Values.Where(result => result is not null))
        {
            result.Value = null;
        }

        foreach (var field in session.ExtractedData.Values.Where(field => field is not null))
        {
            field.Values = [];
        }

        foreach (var evaluation in evaluations)
        {
            evaluation.Summary = null;

            foreach (var criterion in evaluation.Criteria)
            {
                criterion.Reason = null;
            }

            foreach (var issue in evaluation.Issues)
            {
                issue.Detail = null;
                issue.Quote = null;
            }
        }

        if (extractedData is not null)
        {
            foreach (var key in extractedData.Values.Keys.ToList())
            {
                extractedData.Values[key] = [];
            }
        }
    }

    private async Task<AIChatSessionExtractedDataRecord> FindExtractedDataAsync(AIChatSession session, CancellationToken cancellationToken)
    {
        // Its records are read by profile and the session's start, so the window around the start finds this
        // session's record.
        var store = GetExtractedDataStore();

        if (store is null)
        {
            return null;
        }

        var records = await store.GetAsync(session.ProfileId, session.CreatedUtc.AddDays(-1), session.CreatedUtc.AddDays(1), cancellationToken);

        return records.FirstOrDefault(record => string.Equals(record.SessionId, session.SessionId, StringComparison.Ordinal));
    }

    private IAIChatSessionExtractedDataStore GetExtractedDataStore()
        => _options.IncludeExtractedDataRecords ? _serviceProvider.GetService<IAIChatSessionExtractedDataStore>() : null;

    private static void Append(StringBuilder builder, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            builder.AppendLine(text);
        }
    }
}

/// <summary>
/// The personal details the utility model found.
/// </summary>
public sealed class AIPersonalDataFindings
{
    /// <summary>
    /// Gets or sets the details.
    /// </summary>
    public List<AIPersonalDataTerm> Items { get; set; } = [];
}
