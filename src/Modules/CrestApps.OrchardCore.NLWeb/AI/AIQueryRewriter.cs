using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.NLWeb.Protocol;
using CrestApps.OrchardCore.NLWeb.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// Rewrites a follow-up question with the conversation's context, so the search finds what the caller means: after
/// "vegetarian recipes", "with cinnamon?" becomes "vegetarian recipes with cinnamon".
/// </summary>
internal sealed class AIQueryRewriter : INLWebQueryProcessor
{
    private readonly NLWebChatClientFactory _chatClientFactory;
    private readonly ITemplateService _templateService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQueryRewriter"/> class.
    /// </summary>
    /// <param name="chatClientFactory">The NLWeb chat client factory.</param>
    /// <param name="templateService">The prompt template service.</param>
    /// <param name="logger">The logger.</param>
    public AIQueryRewriter(
        NLWebChatClientFactory chatClientFactory,
        ITemplateService templateService,
        ILogger<AIQueryRewriter> logger)
    {
        _chatClientFactory = chatClientFactory;
        _templateService = templateService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public async Task ProcessAsync(NLWebAskContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = NLWebSettingsQueryProcessor.GetSettings(context);
        var request = context.Request;

        if (!settings.RewriteQueries ||
            !request.Context.HasContext ||
            !string.IsNullOrEmpty(request.Query.DecontextualizedText))
        {
            return;
        }

        try
        {
            var client = await _chatClientFactory.CreateAsync(settings);

            if (client is null)
            {
                return;
            }

            var systemPrompt = await _templateService.RenderAsync(NLWebTemplateIds.Decontextualize, cancellationToken: cancellationToken);
            var input = new JsonObject
            {
                ["query"] = request.Query.Text,
                ["previousQueries"] = new JsonArray([.. request.Context.Previous.Select(previous => JsonValue.Create(previous))]),
                ["context"] = request.Context.Text,
                ["memory"] = new JsonArray([.. request.Context.Memory.Select(memory => JsonValue.Create(memory))]),
            };

            var response = await client.GetResponseAsync<NLWebQueryRewrite>(
                [
                    new ChatMessage(ChatRole.System, systemPrompt),
                    new ChatMessage(ChatRole.User, input.ToJsonString(NLWebJson.SerializerOptions)),
                ],
                JsonSerializerOptions.Web,
                cancellationToken: cancellationToken);

            if (!response.TryGetResult(out var rewrite) || string.IsNullOrWhiteSpace(rewrite?.Query))
            {
                return;
            }

            context.QueryText = rewrite.Query.Trim();

            if (string.IsNullOrEmpty(context.ItemType) && !string.IsNullOrWhiteSpace(rewrite.ItemType))
            {
                context.ItemType = rewrite.ItemType.Trim();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The question is still searched as it was asked.
            _logger.LogWarning(ex, "The NLWeb question could not be rewritten with its conversation's context.");
        }
    }
}
