using CrestApps.AI;
using CrestApps.AI.Models;
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
    public string ChatDeploymentId { get; set; }
    public string UtilityDeploymentId { get; set; }
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

    // Tool selection for Profile templates.
    public string[] SelectedToolNames { get; set; } = [];
    public List<ToolSelectionItem> AvailableTools { get; set; } = [];
    public string[] SelectedA2AConnectionIds { get; set; } = [];
    public List<A2AConnectionSelectionItem> AvailableA2AConnections { get; set; } = [];

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
                model.ChatDeploymentId = metadata.ChatDeploymentId;
                model.UtilityDeploymentId = metadata.UtilityDeploymentId;
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

            template.Put(new ProfileTemplateMetadata
            {
                ProfileType = ProfileType,
                SystemMessage = SystemMessage,
                ChatDeploymentId = ChatDeploymentId,
                UtilityDeploymentId = UtilityDeploymentId,
                OrchestratorName = OrchestratorName,
                TitleType = TitleType,
                Temperature = Temperature,
                TopP = TopP,
                FrequencyPenalty = FrequencyPenalty,
                PresencePenalty = PresencePenalty,
                MaxOutputTokens = MaxOutputTokens,
                PastMessagesCount = PastMessagesCount,
                WelcomeMessage = WelcomeMessage,
                PromptTemplate = PromptTemplate,
                PromptSubject = PromptSubject,
                ToolNames = toolNames?.Length > 0 ? toolNames : [],
                A2AConnectionIds = SelectedA2AConnectionIds?
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray() ?? [],
            });
        }
    }
}
