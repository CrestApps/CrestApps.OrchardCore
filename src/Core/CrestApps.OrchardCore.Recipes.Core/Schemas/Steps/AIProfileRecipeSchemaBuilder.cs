using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

internal static class AIProfileRecipeSchemaBuilder
{
    internal static JsonSchemaBuilder BuildPropertiesSchema(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description(description)
            .Properties(GetProfilePropertyEntries().ToDictionary(property => property.Name, property => property.Schema))
            .AdditionalProperties(true);

    internal static JsonSchemaBuilder BuildTemplatePropertiesSchema(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description(description)
            .Properties(
                GetProfilePropertyEntries()
                    .Concat(GetTemplatePropertyEntries())
                    .Concat(GetSettingsEntries())
                    .ToDictionary(property => property.Name, property => property.Schema))
            .AdditionalProperties(true);

    internal static JsonSchemaBuilder BuildSettingsSchema(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description(description)
            .Properties(GetSettingsEntries().ToDictionary(property => property.Name, property => property.Schema))
            .AdditionalProperties(true);

    private static IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetProfilePropertyEntries()
        =>
        [
            ("AgentMetadata", BuildAgentMetadataSchema()),
            ("AIProfileMetadata", BuildAIProfileMetadataSchema()),
            ("FunctionInvocationMetadata", BuildFunctionInvocationMetadataSchema()),
            ("AgentInvocationMetadata", BuildAgentInvocationMetadataSchema()),
            ("PromptTemplateMetadata", BuildPromptTemplateMetadataSchema()),
            ("AnalyticsMetadata", BuildAnalyticsMetadataSchema()),
            ("DataSourceMetadata", BuildDataSourceMetadataSchema()),
            ("AIDataSourceRagMetadata", BuildAIDataSourceRagMetadataSchema()),
            ("AIProfileSessionDocumentsMetadata", BuildAIProfileSessionDocumentsMetadataSchema()),
            ("DocumentsMetadata", BuildDocumentsMetadataSchema()),
            ("MemoryMetadata", BuildMemoryMetadataSchema()),
            ("ClaudeSessionMetadata", BuildClaudeSessionMetadataSchema()),
            ("CopilotSessionMetadata", BuildCopilotSessionMetadataSchema()),
            ("AIProfileMcpMetadata", BuildConnectionIdsMetadataSchema("Known MCP connection selections for this profile.")),
            ("AIProfileA2AMetadata", BuildConnectionIdsMetadataSchema("Known A2A connection selections for this profile.")),
        ];

    private static IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetSettingsEntries()
        =>
        [
            ("AIProfileSettings", BuildAIProfileSettingsSchema()),
            ("ResponseHandlerProfileSettings", BuildResponseHandlerProfileSettingsSchema()),
            ("AIChatProfileSettings", BuildAIChatProfileSettingsSchema()),
            ("AIProfileDataExtractionSettings", BuildAIProfileDataExtractionSettingsSchema()),
            ("AIProfilePostSessionSettings", BuildAIProfilePostSessionSettingsSchema()),
            ("ChatModeProfileSettings", BuildChatModeProfileSettingsSchema()),
        ];

    private static IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTemplatePropertyEntries()
        =>
        [
            ("ProfileTemplateMetadata", BuildProfileTemplateMetadataSchema()),
            ("SystemPromptTemplateMetadata", BuildSystemPromptTemplateMetadataSchema()),
        ];

    private static JsonSchemaBuilder BuildAgentMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known agent metadata for agent profiles.")
            .Properties(
                ("Availability", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("OnDemand", "AlwaysAvailable")
                    .Description("Controls whether the agent is only available when selected or always injected into orchestration.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAIProfileMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known prompt and model tuning metadata for the profile.")
            .Properties(
                ("InitialPrompt", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional initial prompt automatically injected for chat profiles.")),
                ("SystemMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The system instructions for the profile.")),
                ("Temperature", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Sampling temperature.")),
                ("TopP", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Nucleus sampling threshold.")),
                ("FrequencyPenalty", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Reduces frequent token repetition.")),
                ("PresencePenalty", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Encourages topic diversity.")),
                ("MaxTokens", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Maximum output tokens for responses.")),
                ("PastMessagesCount", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Number of prior chat messages to include in context.")),
                ("UseCaching", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether distributed caching is enabled for the profile when the tenant allows it.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildFunctionInvocationMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known tool selection metadata for the profile.")
            .Properties(
                ("Names", BuildStringArraySchema("Tool names that can be invoked by this profile.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAgentInvocationMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known on-demand agent selections for the profile.")
            .Properties(
                ("Names", BuildStringArraySchema("Agent profile names that can be invoked by this profile.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildPromptTemplateMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known prompt template selections applied before or alongside the system message.")
            .Properties(
                ("Templates", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("TemplateId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Prompt template identifier.")),
                            ("Parameters", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Description("Template parameter values. Keys are parameter names and values are strings.")
                                .AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String))))
                        .AdditionalProperties(true))
                    .Description("Ordered prompt template selections for the profile.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAnalyticsMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known analytics and outcome tracking metadata for chat profiles.")
            .Properties(
                ("EnableSessionMetrics", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Enables per-session metrics collection.")),
                ("EnableAIResolutionDetection", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Enables AI resolution detection.")),
                ("EnableConversionMetrics", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Enables conversion goal scoring.")),
                ("ConversionGoals", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique goal key.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Goal description used for scoring guidance.")),
                            ("MinScore", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Minimum accepted score.")),
                            ("MaxScore", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Maximum accepted score.")))
                        .AdditionalProperties(true))
                    .Description("Configured conversion goals for chat analytics.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildDataSourceMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known external data source selection for retrieval augmentation.")
            .Properties(
                ("DataSourceId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Identifier of the selected AI data source.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAIDataSourceRagMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known retrieval augmentation parameters for an attached AI data source.")
            .Properties(
                ("Strictness", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Retrieval strictness value.")),
                ("TopNDocuments", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How many documents to retrieve.")),
                ("IsInScope", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether answers must stay within the retrieved documents.")),
                ("Filter", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional OData filter applied to retrieved data.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAIProfileSessionDocumentsMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known session upload capabilities for the profile.")
            .Properties(
                ("AllowSessionDocuments", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Allows session document uploads.")),
                ("AllowSessionImageUploads", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Allows session image uploads.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildDocumentsMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known profile document attachments and retrieval behavior.")
            .Properties(
                ("Documents", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("DocumentId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Stored document identifier.")),
                            ("FileName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Original file name.")),
                            ("ContentType", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Document MIME type.")),
                            ("FileSize", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Document size in bytes.")))
                        .AdditionalProperties(true))
                    .Description("Documents explicitly attached to the profile.")),
                ("DocumentTopN", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How many top document matches to include.")),
                ("RetrievalMode", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("Chunk", "Hierarchical")
                    .Description("Document retrieval strategy.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildMemoryMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known AI memory configuration for the profile.")
            .Properties(
                ("EnableUserMemory", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether user memory is enabled for the profile.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildClaudeSessionMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known Claude orchestrator overrides when the AI Claude Orchestrator feature is enabled.")
            .Properties(
                ("ClaudeModel", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Claude model override for this profile.")),
                ("EffortLevel", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("Default", "Low", "Medium", "High")
                    .Description("Claude reasoning effort override.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildCopilotSessionMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known Copilot orchestrator overrides when the AI Copilot Orchestrator feature is enabled.")
            .Properties(
                ("CopilotModel", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Copilot model override for this profile.")),
                ("IsAllowAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether Copilot should run with the allow-all flag.")),
                ("ReasoningEffort", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("Default", "Low", "Medium", "High")
                    .Description("Copilot reasoning effort override.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildConnectionIdsMetadataSchema(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description(description)
            .Properties(
                ("ConnectionIds", BuildStringArraySchema("Selected connection identifiers.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildProfileTemplateMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known profile-template metadata stored when the template Source is Profile. These values seed the generated AIProfile before explicit recipe overrides are applied.")
            .Properties(
                ("ProfileType", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("Chat", "Utility", "TemplatePrompt", "Agent")
                    .Description("Profile type created from this template.")),
                ("ChatDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Chat deployment name used when the generated profile needs a chat-capable model.")),
                ("UtilityDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Utility deployment name used when the generated profile needs a utility or background model.")),
                ("OrchestratorName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Orchestrator name applied to generated profiles.")),
                ("InitialResponseHandlerName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional response handler that runs before the main orchestrator.")),
                ("TitleType", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("InitialPrompt", "Generated")
                    .Description("How new session titles should be produced for generated profiles.")),
                ("WelcomeMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Welcome message shown when the generated profile starts a new chat session.")),
                ("PromptTemplate", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional Liquid prompt template copied into the generated profile.")),
                ("PromptSubject", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional subject used with the prompt template.")),
                ("SystemMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Base system message copied into generated profiles.")),
                ("Temperature", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Sampling temperature applied to generated profiles.")),
                ("TopP", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Nucleus sampling threshold applied to generated profiles.")),
                ("FrequencyPenalty", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Frequency penalty applied to generated profiles.")),
                ("PresencePenalty", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Presence penalty applied to generated profiles.")),
                ("MaxOutputTokens", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Maximum output tokens copied into generated profiles.")),
                ("PastMessagesCount", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Number of prior chat messages that generated profiles should include in context.")),
                ("ToolNames", BuildStringArraySchema("Tool names automatically selected for generated profiles.")),
                ("AgentNames", BuildStringArraySchema("On-demand agent profile names automatically selected for generated profiles.")),
                ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Profile description copied into the generated profile.")),
                ("AgentAvailability", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("OnDemand", "AlwaysAvailable")
                    .Description("Agent availability applied when the generated profile type is Agent.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildSystemPromptTemplateMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known system-prompt template metadata stored when the template Source is SystemPrompt.")
            .Properties(
                ("SystemMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("System message contributed by this reusable system-prompt template.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAIProfileSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known core profile settings.")
            .Properties(
                ("LockSystemMessage", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Prevents recipe or editor updates from changing the system message.")),
                ("IsRemovable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Controls whether the profile can be deleted from the admin UI.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildResponseHandlerProfileSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known response handler settings.")
            .Properties(
                ("InitialResponseHandlerName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Initial non-AI response handler to invoke before the main orchestrator.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAIChatProfileSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known chat UI settings.")
            .Properties(
                ("IsOnAdminMenu", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the chat profile appears on the Orchard admin menu.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAIProfileDataExtractionSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known session data extraction settings.")
            .Properties(
                ("EnableDataExtraction", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Enables extracting structured values from chat sessions.")),
                ("ExtractionCheckInterval", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How often data extraction runs during the session.")),
                ("SessionInactivityTimeoutInMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How long the session must be inactive before final extraction runs.")),
                ("DataExtractionEntries", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique extraction key.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("What value should be extracted.")),
                            ("AllowMultipleValues", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether multiple values can be captured.")),
                            ("IsUpdatable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether later session turns can update the extracted value.")))
                        .AdditionalProperties(true))
                    .Description("Structured extraction definitions.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAIProfilePostSessionSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known post-session processing settings.")
            .Properties(
                ("EnablePostSessionProcessing", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Enables deferred post-session tasks.")),
                ("ToolNames", BuildStringArraySchema("Tools available to post-session processing.")),
                ("PostSessionTasks", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique task key.")),
                            ("Type", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Enum("Semantic", "PredefinedOptions")
                                .Description("How the task result is generated.")),
                            ("Instructions", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Instructions for the task.")),
                            ("AllowMultipleValues", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the task can return multiple values.")),
                            ("Options", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Value", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Predefined option value.")),
                                        ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Human-readable description for the option.")))
                                    .AdditionalProperties(true))
                                .Description("Predefined options used when Type is PredefinedOptions.")))
                        .AdditionalProperties(true))
                    .Description("Tasks executed after the chat session closes.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildChatModeProfileSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known chat interaction mode settings.")
            .Properties(
                ("ChatMode", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("TextInput", "AudioInput", "Conversation")
                    .Description("Preferred input mode for this profile.")),
                ("VoiceName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional speech synthesis voice for conversation mode.")),
                ("EnableTextToSpeechPlayback", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether assistant responses should be played back with text-to-speech.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildStringArraySchema(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description(description);
}
