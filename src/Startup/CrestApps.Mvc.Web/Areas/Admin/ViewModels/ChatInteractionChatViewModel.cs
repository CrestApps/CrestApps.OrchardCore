using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.ViewModels;

public sealed class ChatInteractionChatViewModel
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
    public List<A2AConnectionSelectionItem> AvailableA2AConnections { get; set; } = [];

    // MCP Connections
    public string[] SelectedMcpConnectionIds { get; set; } = [];
    public List<McpConnectionSelectionItem> AvailableMcpConnections { get; set; } = [];

    // AI Tools
    public string[] SelectedToolNames { get; set; } = [];
    public List<ToolSelectionItem> AvailableTools { get; set; } = [];

    // AI Agents
    public string[] SelectedAgentNames { get; set; } = [];
    public List<AgentSelectionItem> AvailableAgents { get; set; } = [];

    // Prompt Templates
    public List<PromptTemplateSelectionItem> PromptTemplates { get; set; } = [];
    public List<PromptTemplateOptionItem> AvailablePromptTemplates { get; set; } = [];

    // Documents
    public int? DocumentTopN { get; set; }
    public bool HasDocumentIndexConfiguration { get; set; }
    public string DocumentIndexProfileName { get; set; }

    // Data Sources
    public string DataSourceId { get; set; }
    public List<SelectListItem> DataSources { get; set; } = [];

    // Dropdowns
    public List<SelectListItem> Deployments { get; set; } = [];
    public List<SelectListItem> Orchestrators { get; set; } = [];

    // Copilot
    public string CopilotModel { get; set; }
    public bool CopilotIsAllowAll { get; set; }
    public bool CopilotIsConfigured { get; set; }
    public bool CopilotIsAuthenticated { get; set; }
    public string CopilotGitHubUsername { get; set; }
    public int CopilotAuthenticationType { get; set; }
    public List<SelectListItem> CopilotAvailableModels { get; set; } = [];

    // Existing messages for the chat
    public object[] ExistingMessages { get; set; } = [];
}
