using CrestApps.Core.AI.Models;
using CrestApps.Core.Mvc.Web.Areas.A2A.ViewModels;
using CrestApps.Core.Mvc.Web.Areas.AI.ViewModels;
using CrestApps.Core.Mvc.Web.Areas.Mcp.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.ChatInteractions.ViewModels;

internal sealed class ChatInteractionChatViewModel
{
    public string ItemId { get; set; }

    public string Title { get; set; }

    public string ChatDeploymentName { get; set; }

    public string OrchestratorName { get; set; }

    public string SystemMessage { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public int? MaxTokens { get; set; }

    public int? PastMessagesCount { get; set; }

    // A2A Connections
    public string[] SelectedA2AConnectionIds { get; set; } = [];

    public IEnumerable<A2AConnectionSelectionItem> AvailableA2AConnections { get; set; } = [];

    // MCP Connections
    public string[] SelectedMcpConnectionIds { get; set; } = [];
    public IEnumerable<McpConnectionSelectionItem> AvailableMcpConnections { get; set; } = [];

    // AI Tools
    public string[] SelectedToolNames { get; set; } = [];

    public List<ToolSelectionItem> AvailableTools { get; set; } = [];

    // AI Agents
    public string[] SelectedAgentNames { get; set; } = [];

    public List<AgentSelectionItem> AvailableAgents { get; set; } = [];

    // Prompt Templates
    public IEnumerable<PromptTemplateSelectionItem> PromptTemplates { get; set; } = [];

    public IEnumerable<PromptTemplateOptionItem> AvailablePromptTemplates { get; set; } = [];

    public bool HasDocumentIndexConfiguration { get; set; }

    public string DocumentIndexProfileName { get; set; }

    public IEnumerable<ChatDocumentInfo> Documents { get; set; } = [];

    // Data Sources
    public string DataSourceId { get; set; }

    public int? DataSourceStrictness { get; set; }

    public int? DataSourceTopNDocuments { get; set; }

    public bool DataSourceIsInScope { get; set; }

    public string DataSourceFilter { get; set; }

    // Copilot
    public string CopilotModel { get; set; }

    public bool CopilotIsAllowAll { get; set; }

    public bool CopilotIsConfigured { get; set; }

    public bool CopilotIsAuthenticated { get; set; }

    public string CopilotGitHubUsername { get; set; }

    public int CopilotAuthenticationType { get; set; }

    // Existing messages for the chat
    public object[] ExistingMessages { get; set; } = [];

    public ChatMode ChatMode { get; set; } = ChatMode.TextInput;

    public bool SpeechToTextEnabled { get; set; }

    public bool ConversationModeEnabled { get; set; }

    public bool TextToSpeechEnabled { get; set; }

    public string TextToSpeechVoiceName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> DataSources { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> Orchestrators { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> CopilotAvailableModels { get; set; } = [];
}
