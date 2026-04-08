using System.Text.Json;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Mvc.Web.Areas.A2A.ViewModels;
using CrestApps.Core.Mvc.Web.Areas.ChatInteractions.ViewModels;
using CrestApps.Core.Mvc.Web.Areas.Mcp.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.AI.ViewModels;

public sealed class AIProfileViewModel
{
    // Basic Info
    public string ItemId { get; set; }

    public string Name { get; set; }

    public string DisplayText { get; set; }

    public AIProfileType Type { get; set; }

    public string Source { get; set; }

    public string ChatDeploymentName { get; set; }

    public string UtilityDeploymentName { get; set; }

    public string OrchestratorName { get; set; }

    public string PromptTemplate { get; set; }

    public string PromptSubject { get; set; }

    public string Description { get; set; }

    public AISessionTitleType? TitleType { get; set; }

    // Chat-type interaction fields
    public string WelcomeMessage { get; set; }

    public bool AddInitialPrompt { get; set; }

    public string InitialPrompt { get; set; }

    public ChatMode ChatMode { get; set; }

    public string VoiceName { get; set; }

    // AI Parameters (from AIProfileMetadata)
    public string SystemMessage { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public int? MaxTokens { get; set; }

    public int? PastMessagesCount { get; set; }
    public bool UseCaching { get; set; } = true;

    // Settings (from AIProfileSettings)
    public bool LockSystemMessage { get; set; }
    public bool IsListable { get; set; } = true;
    public bool IsRemovable { get; set; } = true;

    // AI Tools
    public string[] SelectedToolNames { get; set; } = [];
    public List<ToolSelectionItem> AvailableTools { get; set; } = [];

    // AI Agents
    public string[] SelectedAgentNames { get; set; } = [];
    public List<AgentSelectionItem> AvailableAgents { get; set; } = [];

    // Data Source
    public string DataSourceId { get; set; }
    public int? DataSourceStrictness { get; set; }
    public int? DataSourceTopNDocuments { get; set; }
    public bool DataSourceIsInScope { get; set; }
    public string DataSourceFilter { get; set; }

    // A2A Connections
    public string[] SelectedA2AConnectionIds { get; set; } = [];
    public List<A2AConnectionSelectionItem> AvailableA2AConnections { get; set; } = [];

    // MCP Connections
    public string[] SelectedMcpConnectionIds { get; set; } = [];
    public List<McpConnectionSelectionItem> AvailableMcpConnections { get; set; } = [];

    // Prompt Templates
    public List<PromptTemplateSelectionItem> PromptTemplates { get; set; } = [];
    public List<PromptTemplateOptionItem> AvailablePromptTemplates { get; set; } = [];

    // Documents
    public List<DocumentItem> AttachedDocuments { get; set; } = [];
    public int? DocumentTopN { get; set; }

    public bool AllowSessionDocuments { get; set; }

    public bool HasDocumentIndexConfiguration { get; set; }

    public string DocumentIndexProfileName { get; set; }

    // Data Extraction
    public bool EnableDataExtraction { get; set; }
    public int ExtractionCheckInterval { get; set; } = 1;
    public int SessionInactivityTimeoutInMinutes { get; set; } = 30;
    public List<DataExtractionEntryItem> DataExtractionEntries { get; set; } = [];

    // Session Metrics
    public bool EnableSessionMetrics { get; set; }
    public bool EnableAIResolutionDetection { get; set; } = true;
    public bool EnableConversionMetrics { get; set; }
    public List<ConversionGoalItem> ConversionGoals { get; set; } = [];

    // Post Session Processing
    public bool EnablePostSessionProcessing { get; set; }
    public List<PostSessionTaskItem> PostSessionTasks { get; set; } = [];

    // Template
    public string SelectedTemplateId { get; set; }

    // Memory
    public bool EnableUserMemory { get; set; }

    // Copilot
    public string CopilotModel { get; set; }

    public bool CopilotIsAllowAll { get; set; }

    public bool CopilotIsConfigured { get; set; }

    public bool CopilotIsAuthenticated { get; set; }

    public string CopilotGitHubUsername { get; set; }

    public int CopilotAuthenticationType { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> DataSources { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> Orchestrators { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> ChatDeployments { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> UtilityDeployments { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> Templates { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> AvailableProfileTemplates { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> CopilotAvailableModels { get; set; } = [];

    public static AIProfileViewModel FromProfile(AIProfile profile)
    {
        var metadata = profile.As<AIProfileMetadata>();
        var settings = profile.GetSettings<AIProfileSettings>();
        var toolMetadata = profile.As<FunctionInvocationMetadata>();
        var docMetadata = profile.As<DocumentsMetadata>();
        var sessionDocMetadata = profile.As<AIProfileSessionDocumentsMetadata>();
        var dataExtractionSettings = profile.GetSettings<AIProfileDataExtractionSettings>();
        var analyticsMetadata = profile.As<AnalyticsMetadata>();
        var postSessionSettings = profile.GetSettings<AIProfilePostSessionSettings>();
        var memoryMetadata = profile.GetMemoryMetadata();
        profile.TryGetSettings<ChatModeProfileSettings>(out var chatModeSettings);
        var a2aMetadata = profile.As<AIProfileA2AMetadata>();
        var mcpMetadata = profile.As<AIProfileMcpMetadata>();
        var promptMetadata = profile.As<PromptTemplateMetadata>();
        var dataSourceRagMetadata = profile.As<AIDataSourceRagMetadata>();

        var vm = new AIProfileViewModel
        {
            ItemId = profile.ItemId,
            Name = profile.Name,
            DisplayText = profile.DisplayText,
            Type = profile.Type,
            Source = profile.Source,
            ChatDeploymentName = profile.ChatDeploymentName,
            UtilityDeploymentName = profile.UtilityDeploymentName,
            OrchestratorName = profile.OrchestratorName,
            WelcomeMessage = profile.WelcomeMessage,
            PromptTemplate = profile.PromptTemplate,
            PromptSubject = profile.PromptSubject,
            Description = profile.Description,
            TitleType = profile.TitleType,

            AddInitialPrompt = !string.IsNullOrEmpty(metadata.InitialPrompt),
            InitialPrompt = metadata.InitialPrompt,
            ChatMode = chatModeSettings?.ChatMode ?? ChatMode.TextInput,
            VoiceName = chatModeSettings?.VoiceName,

            SystemMessage = metadata.SystemMessage,
            Temperature = metadata.Temperature,
            TopP = metadata.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty,
            MaxTokens = metadata.MaxTokens,
            PastMessagesCount = metadata.PastMessagesCount,
            UseCaching = metadata.UseCaching,

            LockSystemMessage = settings.LockSystemMessage,
            IsListable = settings.IsListable,
            IsRemovable = settings.IsRemovable,

            SelectedToolNames = toolMetadata?.Names ?? [],
            SelectedAgentNames = profile.As<AgentInvocationMetadata>().Names ?? [],
            DataSourceId = profile.As<DataSourceMetadata>().DataSourceId,
            DataSourceStrictness = dataSourceRagMetadata.Strictness,
            DataSourceTopNDocuments = dataSourceRagMetadata.TopNDocuments,
            DataSourceIsInScope = dataSourceRagMetadata.IsInScope,
            DataSourceFilter = dataSourceRagMetadata.Filter,
            SelectedA2AConnectionIds = a2aMetadata?.ConnectionIds ?? [],
            SelectedMcpConnectionIds = mcpMetadata?.ConnectionIds ?? [],

            PromptTemplates = (promptMetadata.Templates ?? [])
                .Where(t => !string.IsNullOrWhiteSpace(t.TemplateId))
                .Select(t => new PromptTemplateSelectionItem
                {
                    TemplateId = t.TemplateId,
                    PromptParameters = t.Parameters is { Count: > 0 }
                ? System.Text.Json.JsonSerializer.Serialize(t.Parameters)
                : null,
                })
            .ToList(),

            DocumentTopN = docMetadata?.DocumentTopN,
            AllowSessionDocuments = sessionDocMetadata?.AllowSessionDocuments ?? false,
            AttachedDocuments = (docMetadata?.Documents ?? []).Select(d => new DocumentItem
            {
                DocumentId = d.DocumentId,
                FileName = d.FileName,
                ContentType = d.ContentType,
                FileSize = d.FileSize,
            }).ToList(),

            EnableDataExtraction = dataExtractionSettings.EnableDataExtraction,
            ExtractionCheckInterval = dataExtractionSettings.ExtractionCheckInterval,
            SessionInactivityTimeoutInMinutes = dataExtractionSettings.SessionInactivityTimeoutInMinutes,
            DataExtractionEntries = dataExtractionSettings.DataExtractionEntries
                .Select(e => new DataExtractionEntryItem
                {
                    Name = e.Name,
                    Description = e.Description,
                    AllowMultipleValues = e.AllowMultipleValues,
                    IsUpdatable = e.IsUpdatable,
                })
            .ToList(),

            EnableSessionMetrics = analyticsMetadata.EnableSessionMetrics,
            EnableAIResolutionDetection = analyticsMetadata.EnableAIResolutionDetection,
            EnableConversionMetrics = analyticsMetadata.EnableConversionMetrics,
            ConversionGoals = analyticsMetadata.ConversionGoals
                .Select(g => new ConversionGoalItem
                {
                    Name = g.Name,
                    Description = g.Description,
                    MinScore = g.MinScore,
                    MaxScore = g.MaxScore,
                })
            .ToList(),

            EnablePostSessionProcessing = postSessionSettings.EnablePostSessionProcessing,
            PostSessionTasks = postSessionSettings.PostSessionTasks.Select(t => new PostSessionTaskItem
            {
                Name = t.Name,
                Type = t.Type,
                Instructions = t.Instructions,
                AllowMultipleValues = t.AllowMultipleValues,
                Options = string.Join(Environment.NewLine, t.Options.Select(o => o.Value)),
                SelectedToolNames = t.ToolNames ?? [],
                SelectedAgentNames = t.AgentNames ?? [],
                SelectedA2AConnectionIds = t.A2AConnectionIds ?? [],
                SelectedMcpConnectionIds = t.McpConnectionIds ?? [],
            }).ToList(),

            EnableUserMemory = memoryMetadata.EnableUserMemory ?? false,
        };

        // Load Copilot metadata if present
        if (profile.TryGet<CopilotSessionMetadata>(out var copilotMeta))
        {
            vm.CopilotModel = copilotMeta.CopilotModel;
            vm.CopilotIsAllowAll = copilotMeta.IsAllowAll;
        }

        return vm;
    }

    public void ApplyTo(AIProfile profile)
    {
        profile.Name = Name;
        profile.DisplayText = DisplayText;
        profile.Type = Type;
        profile.Source = Source;
        profile.ChatDeploymentName = ChatDeploymentName;
        profile.UtilityDeploymentName = UtilityDeploymentName;
        profile.OrchestratorName = OrchestratorName;
        profile.PromptTemplate = PromptTemplate;
        profile.PromptSubject = PromptSubject;
        profile.Description = Description;
        profile.TitleType = TitleType;

        // Welcome message is only used when initial prompt is not enabled.
        profile.WelcomeMessage = AddInitialPrompt ? null : WelcomeMessage;

        profile.Alter<AIProfileMetadata>(m =>
        {
            m.SystemMessage = SystemMessage;
            m.InitialPrompt = AddInitialPrompt ? InitialPrompt?.Trim() : null;
            m.Temperature = Temperature;
            m.TopP = TopP;
            m.FrequencyPenalty = FrequencyPenalty;
            m.PresencePenalty = PresencePenalty;
            m.MaxTokens = MaxTokens;
            m.PastMessagesCount = PastMessagesCount;
            m.UseCaching = UseCaching;
        });

        profile.AlterSettings<AIProfileSettings>(s =>
        {
            s.LockSystemMessage = LockSystemMessage;
            s.IsListable = IsListable;
            s.IsRemovable = IsRemovable;
        });

        profile.AlterSettings<ChatModeProfileSettings>(settings =>
        {
            settings.ChatMode = ChatMode;
            settings.VoiceName = ChatMode == ChatMode.Conversation
                ? VoiceName?.Trim()
                : null;
        });

        var toolNames = SelectedToolNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();

        profile.Alter<FunctionInvocationMetadata>(x =>
        {
            x.Names = toolNames?.Length > 0 ? toolNames : null;
        });

        profile.Alter<AIProfileA2AMetadata>(a =>
        {
            a.ConnectionIds = SelectedA2AConnectionIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? [];
        });

        profile.Alter<AIProfileMcpMetadata>(x =>
        {
            x.ConnectionIds = SelectedMcpConnectionIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? [];
        });

        profile.Alter<AgentInvocationMetadata>(a =>
        {
            var agentNames = SelectedAgentNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();

            a.Names = agentNames?.Length > 0 ? agentNames : [];
        });

        profile.Alter<DataSourceMetadata>(c =>
        {
            c.DataSourceId = DataSourceId;
        });

        profile.Alter<AIDataSourceRagMetadata>(x =>
        {
            x.Strictness = DataSourceStrictness;
            x.TopNDocuments = DataSourceTopNDocuments;
            x.IsInScope = DataSourceIsInScope;
            x.Filter = DataSourceFilter;
        });

        profile.Alter<PromptTemplateMetadata>(metadata =>
        {
            metadata.SetSelections(
                (PromptTemplates ?? [])
                    .Where(t => !string.IsNullOrWhiteSpace(t.TemplateId))
                    .Select(t =>
                    {
                        var entry = new PromptTemplateSelectionEntry { TemplateId = t.TemplateId };

                        if (!string.IsNullOrWhiteSpace(t.PromptParameters))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(t.PromptParameters);

                                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                                {
                                    entry.Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                                    foreach (var prop in doc.RootElement.EnumerateObject())
                                    {
                                        if (prop.Value.ValueKind == JsonValueKind.String)
                                        {
                                            entry.Parameters[prop.Name] = prop.Value.GetString();
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        return entry;
                    }));
        });

        profile.Alter<DocumentsMetadata>(metadata =>
        {
            metadata.DocumentTopN = DocumentTopN;
        });

        profile.Alter<AIProfileSessionDocumentsMetadata>(metadata =>
        {
            metadata.AllowSessionDocuments = AllowSessionDocuments;
        });

        profile.AlterSettings<AIProfileDataExtractionSettings>(s =>
        {
            s.EnableDataExtraction = EnableDataExtraction;
            s.ExtractionCheckInterval = ExtractionCheckInterval;
            s.SessionInactivityTimeoutInMinutes = SessionInactivityTimeoutInMinutes;
            s.DataExtractionEntries = (DataExtractionEntries ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .Select(e => new DataExtractionEntry
            {
                Name = e.Name,
                Description = e.Description,
                AllowMultipleValues = e.AllowMultipleValues,
                IsUpdatable = e.IsUpdatable,
            })
        .ToList();
        });

        profile.Alter<AnalyticsMetadata>(metadata =>
        {
            metadata.EnableSessionMetrics = EnableSessionMetrics;
            metadata.EnableAIResolutionDetection = EnableAIResolutionDetection;
            metadata.EnableConversionMetrics = EnableConversionMetrics;
            metadata.ConversionGoals = (ConversionGoals ?? [])
                .Where(g => !string.IsNullOrWhiteSpace(g.Name))
                .Select(g => new ConversionGoal
                {
                    Name = g.Name,
                    Description = g.Description,
                    MinScore = g.MinScore,
                    MaxScore = g.MaxScore > 0 ? g.MaxScore : 10,
                })
            .ToList();
        });

        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = EnablePostSessionProcessing;
            s.PostSessionTasks = PostSessionTasks
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
                ToolNames = t.SelectedToolNames ?? [],
                AgentNames = t.SelectedAgentNames ?? [],
                A2AConnectionIds = t.SelectedA2AConnectionIds ?? [],
                McpConnectionIds = t.SelectedMcpConnectionIds ?? [],
            }).ToList();
        });

        profile.AlterMemoryMetadata(m =>
        {
            m.EnableUserMemory = EnableUserMemory;
        });

        // Copilot metadata
        if (!string.IsNullOrEmpty(OrchestratorName) &&
            string.Equals(OrchestratorName, CopilotOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            profile.Alter<CopilotSessionMetadata>(metadata =>
            {
                metadata.CopilotModel = CopilotModel;
                metadata.IsAllowAll = CopilotIsAllowAll;
            });
        }
        else
        {
            profile.Remove<CopilotSessionMetadata>();
        }
    }
}

public sealed class ToolSelectionItem
{
    public string Name { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Category { get; set; }

    public bool IsSelected { get; set; }
}

public sealed class DocumentItem
{
    public string DocumentId { get; set; }

    public string FileName { get; set; }

    public string ContentType { get; set; }

    public long FileSize { get; set; }
}

public sealed class PostSessionTaskItem
{
    public string Name { get; set; }

    public PostSessionTaskType Type { get; set; }

    public string Instructions { get; set; }

    public bool AllowMultipleValues { get; set; }

    public string Options { get; set; }

    public string[] SelectedToolNames { get; set; } = [];

    public string[] SelectedAgentNames { get; set; } = [];

    public string[] SelectedA2AConnectionIds { get; set; } = [];

    public string[] SelectedMcpConnectionIds { get; set; } = [];
}

public sealed class PromptTemplateSelectionItem
{
    public string TemplateId { get; set; }

    public string PromptParameters { get; set; }
}

public sealed class PromptTemplateOptionItem
{
    public string TemplateId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Category { get; set; }
    public List<PromptTemplateParameterItem> Parameters { get; set; } = [];
}

public sealed class PromptTemplateParameterItem
{
    public string Name { get; set; }

    public string Description { get; set; }
}

public sealed class DataExtractionEntryItem
{
    public string Name { get; set; }

    public string Description { get; set; }

    public bool AllowMultipleValues { get; set; }

    public bool IsUpdatable { get; set; }
}

public sealed class ConversionGoalItem
{
    public string Name { get; set; }

    public string Description { get; set; }

    public int MinScore { get; set; }
    public int MaxScore { get; set; } = 10;
}
