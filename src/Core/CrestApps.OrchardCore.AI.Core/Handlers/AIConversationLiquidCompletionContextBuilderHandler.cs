using System.Text.Encodings.Web;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Renders the system message as a Liquid template with the conversation's values, so a profile's instructions can
/// say <c>{{ Variables.first_name }}</c> or <c>{{ Contact.DisplayText }}</c> on every channel.
/// </summary>
/// <remarks>
/// Prompt templates and persona instructions are rendered by CrestApps.Core; the profile's own text is not, so this
/// renders the composed message once it is built. A message without Liquid markup is left alone, and a template that
/// fails to render is logged and used as written rather than failing the conversation.
/// </remarks>
public sealed class AIConversationLiquidCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    private static readonly TemplateOptions _templateOptions = new();

    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationLiquidCompletionContextBuilderHandler"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager.</param>
    /// <param name="logger">The logger.</param>
    public AIConversationLiquidCompletionContextBuilderHandler(
        ILiquidTemplateManager liquidTemplateManager,
        ILogger<AIConversationLiquidCompletionContextBuilderHandler> logger)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task BuildingAsync(AICompletionContextBuildingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task BuiltAsync(AICompletionContextBuiltContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var systemMessage = context.Context.SystemMessage;

        if (!HasLiquid(systemMessage))
        {
            return;
        }

        try
        {
            context.Context.SystemMessage = await _liquidTemplateManager.RenderStringAsync(
                systemMessage,
                NullEncoder.Default,
                model: null,
                BuildProperties(AIConversationScope.Resolve(), context.Resource));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The system message could not be rendered as a Liquid template and is used as written.");
        }
    }

    /// <summary>
    /// Builds the values a system message template can read.
    /// </summary>
    /// <param name="conversation">The current conversation, or <see langword="null"/> outside one.</param>
    /// <param name="resource">The profile or interaction the request is built for.</param>
    /// <returns>The template values by name.</returns>
    internal static Dictionary<string, FluidValue> BuildProperties(AIConversationContext conversation, object resource)
    {
        var properties = new Dictionary<string, FluidValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["Variables"] = FluidValue.Create(conversation?.GetVariables() ?? new Dictionary<string, string>(), _templateOptions),
        };

        if (resource is AIProfile profile)
        {
            properties["Profile"] = new ObjectValue(profile);
        }

        if (conversation is null)
        {
            return properties;
        }

        properties["Conversation"] = new ObjectValue(new AIConversationTemplateModel(conversation));

        if (conversation.Session is not null)
        {
            properties["Session"] = new ObjectValue(conversation.Session);
        }

        if (conversation.Contact is not null)
        {
            properties["Contact"] = new ObjectValue(conversation.Contact);
        }

        foreach (var (name, value) in conversation.Values)
        {
            if (value is not null && !properties.ContainsKey(name))
            {
                properties[name] = new ObjectValue(value);
            }
        }

        return properties;
    }

    /// <summary>
    /// Whether a message contains Liquid output or tag markup.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <returns><see langword="true"/> when it has Liquid markup.</returns>
    internal static bool HasLiquid(string text)
        => !string.IsNullOrEmpty(text) &&
            (text.Contains("{{", StringComparison.Ordinal) || text.Contains("{%", StringComparison.Ordinal));
}
