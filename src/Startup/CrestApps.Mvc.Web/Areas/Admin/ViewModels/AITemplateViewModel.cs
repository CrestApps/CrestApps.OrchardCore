using CrestApps.AI;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.ViewModels;

public sealed class AITemplateViewModel
{
    public string ItemId { get; set; }
    public string Name { get; set; }
    public string DisplayText { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public bool IsListable { get; set; } = true;
    public string Source { get; set; } = AITemplateSources.SystemPrompt;

    // SystemPrompt source fields.
    public string SystemMessage { get; set; }

    // Profile source fields.
    public AIProfileType? ProfileType { get; set; }
    public string ChatDeploymentName { get; set; }
    public string UtilityDeploymentName { get; set; }
    public string OrchestratorName { get; set; }
    public AISessionTitleType? TitleType { get; set; }
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
    public float? FrequencyPenalty { get; set; }
    public float? PresencePenalty { get; set; }
    public int? MaxOutputTokens { get; set; }
    public int? PastMessagesCount { get; set; }
    public string WelcomeMessage { get; set; }
    public string PromptTemplate { get; set; }
    public string PromptSubject { get; set; }
    public bool UseCaching { get; set; } = true;
    public bool AddInitialPrompt { get; set; }
    public string InitialPrompt { get; set; }

    // Tool selection for Profile templates.
    public string[] SelectedToolNames { get; set; } = [];
    public List<ToolSelectionItem> AvailableTools { get; set; } = [];
    public string[] SelectedA2AConnectionIds { get; set; } = [];
    public List<A2AConnectionSelectionItem> AvailableA2AConnections { get; set; } = [];

    // Agents.
    public string[] SelectedAgentNames { get; set; } = [];
    public List<AgentSelectionItem> AvailableAgents { get; set; } = [];

    // MCP Connections.
    public string[] SelectedMcpConnectionIds { get; set; } = [];
    public List<McpConnectionSelectionItem> AvailableMcpConnections { get; set; } = [];

    // Documents.
    public bool AllowSessionDocuments { get; set; }
    public int? DocumentTopN { get; set; }

    // Data Extraction.
    public bool EnableDataExtraction { get; set; }
    public int ExtractionCheckInterval { get; set; } = 1;
    public int SessionInactivityTimeoutInMinutes { get; set; } = 30;
    public List<DataExtractionEntryItem> DataExtractionEntries { get; set; } = [];

    // Analytics.
    public bool EnableSessionMetrics { get; set; }
    public bool EnableAIResolutionDetection { get; set; } = true;
    public bool EnableConversionMetrics { get; set; }
    public List<ConversionGoalItem> ConversionGoals { get; set; } = [];

    // Post Session Processing.
    public bool EnablePostSessionProcessing { get; set; }
    public List<PostSessionTaskItem> PostSessionTasks { get; set; } = [];

    // Memory.
    public bool EnableUserMemory { get; set; }

    // Settings.
    public bool IsRemovable { get; set; } = true;
    public bool LockSystemMessage { get; set; }

    // Dropdowns for Profile templates.
    public List<SelectListItem> ChatDeployments { get; set; } = [];
    public List<SelectListItem> UtilityDeployments { get; set; } = [];
    public List<SelectListItem> Orchestrators { get; set; } = [];

    public static AITemplateViewModel FromTemplate(AIProfileTemplate template)
    {
        var model = new AITemplateViewModel
        {
            ItemId = template.ItemId,
            Name = template.Name,
            DisplayText = template.DisplayText,
            Description = template.Description,
            Category = template.Category,
            IsListable = template.IsListable,
            Source = template.Source,
        };

        if (template.Source == AITemplateSources.SystemPrompt)
        {
            var metadata = template.As<SystemPromptTemplateMetadata>();

            if (metadata != null)
            {
                model.SystemMessage = metadata.SystemMessage;
            }
        }
        else if (template.Source == AITemplateSources.Profile)
        {
            var metadata = template.As<ProfileTemplateMetadata>();

            if (metadata != null)
            {
                model.ProfileType = metadata.ProfileType;
                model.SystemMessage = metadata.SystemMessage;
                model.ChatDeploymentName = metadata.ChatDeploymentName;
                model.UtilityDeploymentName = metadata.UtilityDeploymentName;
                model.OrchestratorName = metadata.OrchestratorName;
                model.TitleType = metadata.TitleType;
                model.Temperature = metadata.Temperature;
                model.TopP = metadata.TopP;
                model.FrequencyPenalty = metadata.FrequencyPenalty;
                model.PresencePenalty = metadata.PresencePenalty;
                model.MaxOutputTokens = metadata.MaxOutputTokens;
                model.PastMessagesCount = metadata.PastMessagesCount;
                model.WelcomeMessage = metadata.WelcomeMessage;
                model.PromptTemplate = metadata.PromptTemplate;
                model.PromptSubject = metadata.PromptSubject;
                model.SelectedToolNames = metadata.ToolNames ?? [];
                model.SelectedA2AConnectionIds = metadata.A2AConnectionIds ?? [];
                model.SelectedAgentNames = metadata.AgentNames ?? [];
            }

            var mcpMetadata = template.As<AIProfileMcpMetadata>();
            if (mcpMetadata != null)
            {
                model.SelectedMcpConnectionIds = mcpMetadata.ConnectionIds ?? [];
            }

            var aiMetadata = template.As<AIProfileMetadata>();
            if (aiMetadata != null)
            {
                model.UseCaching = aiMetadata.UseCaching;
                model.AddInitialPrompt = !string.IsNullOrEmpty(aiMetadata.InitialPrompt);
                model.InitialPrompt = aiMetadata.InitialPrompt;
            }

            var sessionDocMetadata = template.As<AIProfileSessionDocumentsMetadata>();
            if (sessionDocMetadata != null)
            {
                model.AllowSessionDocuments = sessionDocMetadata.AllowSessionDocuments;
            }

            var docMetadata = template.As<DocumentsMetadata>();
            if (docMetadata != null)
            {
                model.DocumentTopN = docMetadata.DocumentTopN;
            }

            var dataExtractionSettings = template.As<AIProfileDataExtractionSettings>();
            if (dataExtractionSettings != null)
            {
                model.EnableDataExtraction = dataExtractionSettings.EnableDataExtraction;
                model.ExtractionCheckInterval = dataExtractionSettings.ExtractionCheckInterval;
                model.SessionInactivityTimeoutInMinutes = dataExtractionSettings.SessionInactivityTimeoutInMinutes;
                model.DataExtractionEntries = dataExtractionSettings.DataExtractionEntries
                    .Select(e => new DataExtractionEntryItem
                    {
                        Name = e.Name,
                        Description = e.Description,
                        AllowMultipleValues = e.AllowMultipleValues,
                        IsUpdatable = e.IsUpdatable,
                    })
                    .ToList();
            }

            var analyticsMetadata = template.As<AnalyticsMetadata>();
            if (analyticsMetadata != null)
            {
                model.EnableSessionMetrics = analyticsMetadata.EnableSessionMetrics;
                model.EnableAIResolutionDetection = analyticsMetadata.EnableAIResolutionDetection;
                model.EnableConversionMetrics = analyticsMetadata.EnableConversionMetrics;
                model.ConversionGoals = analyticsMetadata.ConversionGoals
                    .Select(g => new ConversionGoalItem
                    {
                        Name = g.Name,
                        Description = g.Description,
                        MinScore = g.MinScore,
                        MaxScore = g.MaxScore,
                    })
                    .ToList();
            }

            var postSessionSettings = template.As<AIProfilePostSessionSettings>();
            if (postSessionSettings != null)
            {
                model.EnablePostSessionProcessing = postSessionSettings.EnablePostSessionProcessing;
                model.PostSessionTasks = postSessionSettings.PostSessionTasks
                    .Select(t => new PostSessionTaskItem
                    {
                        Name = t.Name,
                        Type = t.Type,
                        Instructions = t.Instructions,
                        AllowMultipleValues = t.AllowMultipleValues,
                        Options = string.Join(Environment.NewLine, t.Options.Select(o => o.Value)),
                    })
                    .ToList();
            }

            var memorySettings = template.As<MemorySettings>();
            if (memorySettings != null)
            {
                model.EnableUserMemory = memorySettings.EnableUserMemory;
            }

            var profileSettings = template.As<AIProfileSettings>();
            if (profileSettings != null)
            {
                model.IsRemovable = profileSettings.IsRemovable;
                model.LockSystemMessage = profileSettings.LockSystemMessage;
            }
        }

        return model;
    }

    public void ApplyTo(AIProfileTemplate template)
    {
        template.Name = Name;
        template.DisplayText = DisplayText;
        template.Description = Description;
        template.Category = Category;
        template.IsListable = IsListable;
        template.Source = Source;

        if (Source == AITemplateSources.SystemPrompt)
        {
            template.Put(new SystemPromptTemplateMetadata
            {
                SystemMessage = SystemMessage,
            });
        }
        else if (Source == AITemplateSources.Profile)
        {
            var toolNames = SelectedToolNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
            var agentNames = SelectedAgentNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();

            template.Put(new ProfileTemplateMetadata
            {
                ProfileType = ProfileType,
                SystemMessage = SystemMessage,
                ChatDeploymentName = ChatDeploymentName,
                UtilityDeploymentName = UtilityDeploymentName,
                OrchestratorName = OrchestratorName,
                TitleType = TitleType,
                Temperature = Temperature,
                TopP = TopP,
                FrequencyPenalty = FrequencyPenalty,
                PresencePenalty = PresencePenalty,
                MaxOutputTokens = MaxOutputTokens,
                PastMessagesCount = PastMessagesCount,
                WelcomeMessage = AddInitialPrompt ? null : WelcomeMessage,
                PromptTemplate = PromptTemplate,
                PromptSubject = PromptSubject,
                ToolNames = toolNames?.Length > 0 ? toolNames : [],
                AgentNames = agentNames?.Length > 0 ? agentNames : [],
                A2AConnectionIds = SelectedA2AConnectionIds?
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray() ?? [],
            });

            template.Put(new AIProfileMcpMetadata
            {
                ConnectionIds = SelectedMcpConnectionIds?
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray() ?? [],
            });

            template.Put(new AIProfileMetadata
            {
                UseCaching = UseCaching,
                InitialPrompt = AddInitialPrompt ? InitialPrompt?.Trim() : null,
            });

            template.Put(new AIProfileSessionDocumentsMetadata
            {
                AllowSessionDocuments = AllowSessionDocuments,
            });

            template.Put(new DocumentsMetadata
            {
                DocumentTopN = DocumentTopN,
            });

            template.Put(new AIProfileDataExtractionSettings
            {
                EnableDataExtraction = EnableDataExtraction,
                ExtractionCheckInterval = ExtractionCheckInterval,
                SessionInactivityTimeoutInMinutes = SessionInactivityTimeoutInMinutes,
                DataExtractionEntries = (DataExtractionEntries ?? [])
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .Select(e => new DataExtractionEntry
                    {
                        Name = e.Name,
                        Description = e.Description,
                        AllowMultipleValues = e.AllowMultipleValues,
                        IsUpdatable = e.IsUpdatable,
                    })
                    .ToList(),
            });

            template.Put(new AnalyticsMetadata
            {
                EnableSessionMetrics = EnableSessionMetrics,
                EnableAIResolutionDetection = EnableAIResolutionDetection,
                EnableConversionMetrics = EnableConversionMetrics,
                ConversionGoals = (ConversionGoals ?? [])
                    .Where(g => !string.IsNullOrWhiteSpace(g.Name))
                    .Select(g => new ConversionGoal
                    {
                        Name = g.Name,
                        Description = g.Description,
                        MinScore = g.MinScore,
                        MaxScore = g.MaxScore > 0 ? g.MaxScore : 10,
                    })
                    .ToList(),
            });

            template.Put(new AIProfilePostSessionSettings
            {
                EnablePostSessionProcessing = EnablePostSessionProcessing,
                PostSessionTasks = (PostSessionTasks ?? [])
                    .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                    .Select(t => new PostSessionTask
                    {
                        Name = t.Name,
                        Type = t.Type,
                        Instructions = t.Instructions,
                        AllowMultipleValues = t.AllowMultipleValues,
                        Options = (t.Options ?? "")
                            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => new PostSessionTaskOption { Value = o.Trim() })
                            .ToList(),
                    })
                    .ToList(),
            });

            template.Put(new MemorySettings
            {
                EnableUserMemory = EnableUserMemory,
            });

            template.Put(new AIProfileSettings
            {
                LockSystemMessage = LockSystemMessage,
                IsListable = IsListable,
                IsRemovable = IsRemovable,
            });
        }
    }
}
