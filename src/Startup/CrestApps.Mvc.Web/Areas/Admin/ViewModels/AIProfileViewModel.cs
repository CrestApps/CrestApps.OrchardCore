using CrestApps.AI.A2A.Models;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.ViewModels;

public sealed class AIProfileViewModel
{
    // Basic Info
    public string ItemId { get; set; }
    public string Name { get; set; }
    public string DisplayText { get; set; }
    public AIProfileType Type { get; set; }
    public string Source { get; set; }
    public string ChatDeploymentId { get; set; }
    public string UtilityDeploymentId { get; set; }
    public string OrchestratorName { get; set; }
    public string PromptTemplate { get; set; }
    public string PromptSubject { get; set; }
    public string Description { get; set; }
    public AISessionTitleType? TitleType { get; set; }

    // Chat-type interaction fields
    public string WelcomeMessage { get; set; }
    public bool AddInitialPrompt { get; set; }
    public string InitialPrompt { get; set; }

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

    // A2A Connections
    public string[] SelectedA2AConnectionIds { get; set; } = [];
    public List<A2AConnectionSelectionItem> AvailableA2AConnections { get; set; } = [];

    // MCP Connections
    public string[] SelectedMcpConnectionIds { get; set; } = [];
    public List<McpConnectionSelectionItem> AvailableMcpConnections { get; set; } = [];

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

    // Session Metrics
    public bool EnableSessionMetrics { get; set; }

    // Post Session Processing
    public bool EnablePostSessionProcessing { get; set; }
    public List<PostSessionTaskItem> PostSessionTasks { get; set; } = [];

    // Dropdowns
    public List<SelectListItem> Orchestrators { get; set; } = [];
    public List<SelectListItem> ChatDeployments { get; set; } = [];
    public List<SelectListItem> UtilityDeployments { get; set; } = [];
    public List<SelectListItem> Templates { get; set; } = [];

    // Template
    public string SelectedTemplateId { get; set; }

    // Memory
    public bool EnableUserMemory { get; set; }

    public static AIProfileViewModel FromProfile(AIProfile profile)
    {
        var metadata = profile.GetSettings<AIProfileMetadata>();
        var settings = profile.GetSettings<AIProfileSettings>();
        var toolMetadata = profile.GetSettings<FunctionInvocationMetadata>();
        var docMetadata = profile.GetSettings<DocumentsMetadata>();
        var sessionDocMetadata = profile.GetSettings<AIProfileSessionDocumentsMetadata>();
        var dataExtractionSettings = profile.GetSettings<AIProfileDataExtractionSettings>();
        var analyticsMetadata = profile.As<AnalyticsMetadata>();
        var postSessionSettings = profile.GetSettings<AIProfilePostSessionSettings>();
        var memorySettings = profile.GetSettings<MemorySettings>();
        var a2aMetadata = profile.As<AIProfileA2AMetadata>();
        var mcpMetadata = profile.As<AIProfileMcpMetadata>();

        return new AIProfileViewModel
        {
            ItemId = profile.ItemId,
            Name = profile.Name,
            DisplayText = profile.DisplayText,
            Type = profile.Type,
            Source = profile.Source,
            ChatDeploymentId = profile.ChatDeploymentId,
            UtilityDeploymentId = profile.UtilityDeploymentId,
            OrchestratorName = profile.OrchestratorName,
            WelcomeMessage = profile.WelcomeMessage,
            PromptTemplate = profile.PromptTemplate,
            PromptSubject = profile.PromptSubject,
            Description = profile.Description,
            TitleType = profile.TitleType,

            AddInitialPrompt = !string.IsNullOrEmpty(metadata.InitialPrompt),
            InitialPrompt = metadata.InitialPrompt,

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
            SelectedA2AConnectionIds = a2aMetadata?.ConnectionIds ?? [],
            SelectedMcpConnectionIds = mcpMetadata?.ConnectionIds ?? [],

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

            EnableSessionMetrics = analyticsMetadata.EnableSessionMetrics,

            EnablePostSessionProcessing = postSessionSettings.EnablePostSessionProcessing,
            PostSessionTasks = postSessionSettings.PostSessionTasks.Select(t => new PostSessionTaskItem
            {
                Name = t.Name,
                Type = t.Type,
                Instructions = t.Instructions,
                AllowMultipleValues = t.AllowMultipleValues,
                Options = string.Join(Environment.NewLine, t.Options.Select(o => o.Value)),
            }).ToList(),

            EnableUserMemory = memorySettings.EnableUserMemory,
        };
    }

    public void ApplyTo(AIProfile profile)
    {
        profile.Name = Name;
        profile.DisplayText = DisplayText;
        profile.Type = Type;
        profile.Source = Source;
        profile.ChatDeploymentId = ChatDeploymentId;
        profile.UtilityDeploymentId = UtilityDeploymentId;
        profile.OrchestratorName = OrchestratorName;
        profile.PromptTemplate = PromptTemplate;
        profile.PromptSubject = PromptSubject;
        profile.Description = Description;
        profile.TitleType = TitleType;

        // Welcome message is only used when initial prompt is not enabled.
        profile.WelcomeMessage = AddInitialPrompt ? null : WelcomeMessage;

        profile.AlterSettings<AIProfileMetadata>(m =>
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

        var toolNames = SelectedToolNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();

        profile.WithSettings(new FunctionInvocationMetadata
        {
            Names = toolNames?.Length > 0 ? toolNames : null,
        });

        profile.Put(new AIProfileA2AMetadata
        {
            ConnectionIds = SelectedA2AConnectionIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? [],
        });

        profile.Put(new AIProfileMcpMetadata
        {
            ConnectionIds = SelectedMcpConnectionIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? [],
        });

        profile.AlterSettings<DocumentsMetadata>(m =>
        {
            m.DocumentTopN = DocumentTopN;
        });

        profile.AlterSettings<AIProfileSessionDocumentsMetadata>(m =>
        {
            m.AllowSessionDocuments = AllowSessionDocuments;
        });

        profile.AlterSettings<AIProfileDataExtractionSettings>(s =>
        {
            s.EnableDataExtraction = EnableDataExtraction;
            s.ExtractionCheckInterval = ExtractionCheckInterval;
            s.SessionInactivityTimeoutInMinutes = SessionInactivityTimeoutInMinutes;
        });

        profile.Put(new AnalyticsMetadata
        {
            EnableSessionMetrics = EnableSessionMetrics,
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
                }).ToList();
        });

        profile.AlterSettings<MemorySettings>(m =>
        {
            m.EnableUserMemory = EnableUserMemory;
        });
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
}
