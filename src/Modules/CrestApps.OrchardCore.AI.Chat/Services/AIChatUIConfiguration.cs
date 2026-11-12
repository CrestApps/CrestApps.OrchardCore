using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Serializes the configuration of a chat surface for <c>ai-chat.js</c> with the AG-UI texts in the user's language: the
/// approval buttons, the tool states, the page context labels, and a friendly name for every tool, so the tool cards
/// show "Delete Content Item" rather than <c>deleteContent</c>.
/// </summary>
public sealed class AIChatUIConfiguration
{
    private readonly AIToolDefinitionOptions _toolDefinitions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatUIConfiguration"/> class.
    /// </summary>
    /// <param name="toolDefinitions">The registered AI tools.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatUIConfiguration(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IStringLocalizer<AIChatUIConfiguration> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        S = stringLocalizer;
    }

    /// <summary>
    /// Serializes a chat configuration (the <c>data-coreai-chat-config</c> value) with the AG-UI texts.
    /// </summary>
    /// <param name="chatConfig">The chat configuration.</param>
    /// <returns>The JSON.</returns>
    public string SerializeChatConfig(object chatConfig)
    {
        var config = ToJsonObject(chatConfig);

        AddTexts(config);

        return config.ToJsonString(JOptions.CamelCase);
    }

    /// <summary>
    /// Serializes a chat widget configuration (the <c>data-coreai-chat-widget-config</c> value), whose chat
    /// configuration is its <c>chatConfig</c> member, with the AG-UI texts.
    /// </summary>
    /// <param name="widgetConfig">The widget configuration.</param>
    /// <returns>The JSON.</returns>
    public string SerializeWidgetConfig(object widgetConfig)
    {
        var config = ToJsonObject(widgetConfig);

        if (config["chatConfig"] is JsonObject chatConfig)
        {
            AddTexts(chatConfig);
        }

        return config.ToJsonString(JOptions.CamelCase);
    }

    /// <summary>
    /// Adds the AG-UI texts to a chat configuration, keeping the values it already has.
    /// </summary>
    /// <param name="chatConfig">The chat configuration.</param>
    public void AddTexts(JsonObject chatConfig)
    {
        ArgumentNullException.ThrowIfNull(chatConfig);

        Set(chatConfig, "aguiThinkingLabel", S["Thinking"]);
        Set(chatConfig, "aguiInputLabel", S["Input"]);
        Set(chatConfig, "aguiResultLabel", S["Result"]);
        Set(chatConfig, "aguiApprovalPrompt", S["Allow this action?"]);
        Set(chatConfig, "aguiAllowLabel", S["Allow"]);
        Set(chatConfig, "aguiDenyLabel", S["Deny"]);
        Set(chatConfig, "aguiAllowedLabel", S["Allowed"]);
        Set(chatConfig, "aguiDeniedLabel", S["Denied"]);
        Set(chatConfig, "aguiWaitingLabel", S["Waiting for your answer"]);
        Set(chatConfig, "aguiRunningLabel", S["Running"]);
        Set(chatConfig, "aguiCurrentPageLabel", S["Current page"]);
        Set(chatConfig, "aguiSelectedTextLabel", S["Text the user selected on the page"]);

        if (chatConfig["aguiToolLabels"] is null)
        {
            chatConfig["aguiToolLabels"] = GetToolLabels();
        }
    }

    private JsonObject GetToolLabels()
    {
        var labels = new JsonObject
        {
            ["page_get_info"] = S["Reading the page"].Value,
            ["page_read_content"] = S["Reading the page's content"].Value,
            ["page_list_links"] = S["Looking at the page's links"].Value,
            ["page_navigate"] = S["Opening a page"].Value,
            ["page_list_form_fields"] = S["Reading the form"].Value,
            ["page_set_form_field"] = S["Filling in a field"].Value,
            ["update_shared_state"] = S["Updating what you share"].Value,
        };

        foreach (var (name, tool) in _toolDefinitions.Tools)
        {
            if (!string.IsNullOrWhiteSpace(tool.Title) && !labels.ContainsKey(name))
            {
                labels[name] = tool.Title;
            }
        }

        return labels;
    }

    private static void Set(JsonObject config, string name, LocalizedString value)
    {
        if (config[name] is null)
        {
            config[name] = value.Value;
        }
    }

    private static JsonObject ToJsonObject(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return JsonSerializer.SerializeToNode(value, JOptions.CamelCase) as JsonObject
            ?? throw new ArgumentException("The configuration must serialize to a JSON object.", nameof(value));
    }
}
