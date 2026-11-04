using System.Text.Json.Nodes;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.NLWeb.Protocol;
using CrestApps.OrchardCore.NLWeb.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// Writes the summary of the results (<c>summarize</c> mode) or an answer to the question written only from the
/// results (<c>generate</c> mode).
/// </summary>
internal sealed class AIResponseGenerator : INLWebResponseGenerator
{
    private readonly NLWebChatClientFactory _chatClientFactory;
    private readonly ITemplateService _templateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIResponseGenerator"/> class.
    /// </summary>
    /// <param name="chatClientFactory">The NLWeb chat client factory.</param>
    /// <param name="templateService">The prompt template service.</param>
    public AIResponseGenerator(
        NLWebChatClientFactory chatClientFactory,
        ITemplateService templateService)
    {
        _chatClientFactory = chatClientFactory;
        _templateService = templateService;
    }

    /// <inheritdoc/>
    public bool CanGenerate(string mode)
        => mode is NLWebProtocol.Modes.Summarize or NLWebProtocol.Modes.Generate;

    /// <inheritdoc/>
    public async Task<string> GenerateAsync(NLWebAskContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = NLWebSettingsQueryProcessor.GetSettings(context);
        var client = await _chatClientFactory.CreateAsync(settings);

        if (client is null || context.Results.Count == 0)
        {
            return null;
        }

        var templateId = context.Mode == NLWebProtocol.Modes.Generate
            ? NLWebTemplateIds.Generate
            : NLWebTemplateIds.Summarize;

        var systemPrompt = await _templateService.RenderAsync(
            templateId,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Language"] = context.Request.Prefer.Language ?? string.Empty,
            },
            cancellationToken);

        var input = new JsonObject
        {
            ["question"] = context.QueryText,
            ["results"] = JsonNode.Parse(NLWebAIPayloads.DescribeResults(context.Results)),
        };

        var response = await client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, input.ToJsonString(NLWebJson.SerializerOptions)),
            ],
            cancellationToken: cancellationToken);

        return response.Text?.Trim();
    }
}
