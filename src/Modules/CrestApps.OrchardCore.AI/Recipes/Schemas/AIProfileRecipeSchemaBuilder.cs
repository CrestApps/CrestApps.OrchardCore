using Json.Schema;

namespace CrestApps.OrchardCore.AI.Recipes.Schemas;

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
            ("AIPersonaMetadata", BuildAIPersonaMetadataSchema()),
            ("AnalyticsMetadata", BuildAnalyticsMetadataSchema()),
            ("DataSourceMetadata", BuildDataSourceMetadataSchema()),
            ("AIDataSourceRagMetadata", BuildAIDataSourceRagMetadataSchema()),
            ("AIProfileSessionDocumentsMetadata", BuildAIProfileSessionDocumentsMetadataSchema()),
            ("DocumentsMetadata", BuildDocumentsMetadataSchema()),
            ("MemoryMetadata", BuildMemoryMetadataSchema()),
            ("ClaudeSessionMetadata", BuildClaudeSessionMetadataSchema()),
            ("CopilotSessionMetadata", BuildCopilotSessionMetadataSchema()),
            ("AIDeploymentParametersMetadata", BuildDeploymentParametersMetadataSchema()),
            ("AIProfileMcpMetadata", BuildMcpMetadataSchema()),
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
            ("AIProfileGuardrailSettings", BuildGuardrailSettingsSchema()),
            ("AIProfileDataPrivacySettings", BuildDataPrivacySettingsSchema()),
            ("AIProfileSpeakingStyleSettings", BuildSpeakingStyleSettingsSchema()),
            ("AIProfileSpeechSettings", BuildSpeechSettingsSchema()),
            ("AIProfileLanguageSettings", BuildLanguageSettingsSchema()),
            ("AIProfileFlowSettings", BuildFlowSettingsSchema()),
            ("AIProfileContactMemorySettings", BuildContactMemorySettingsSchema()),
            ("AIProfileContactFieldMappingSettings", BuildContactFieldMappingSettingsSchema()),
            ("PromptSecurityProfileSettings", BuildPromptSecurityProfileSettingsSchema()),
            ("WebMcpProfileSettings", BuildWebMcpProfileSettingsSchema()),
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
                    .Description("Controls whether the agent is only available when selected or always injected into orchestration.")),
                ("AllowToolInvocation", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("When true, the agent runs its own selected tools when another assistant calls it. When false or missing, it runs without tools.")))
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
                ("Filter", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional OData filter applied to retrieved data.")),
                ("ObjectTypes", BuildStringArraySchema("Optional object types (for example content types) the retrieved documents are limited to. Empty searches every type.")))
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
                    .Description("Document retrieval strategy.")),
                ("MaxIndexableCharacters", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description("Optional limit on the characters of a document that are indexed. Null uses the site default.")),
                ("DescribeFiguresInUploads", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether figures in uploaded documents are described by an AI model while indexing. Null uses the site default.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildMemoryMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known AI memory configuration for the profile.")
            .Properties(
                ("EnableUserMemory", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether user memory is enabled for the profile. Null uses the site default.")),
                ("EnableSessionMemory", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether the profile keeps a memory of the current session. Null uses the site default.")),
                ("EnablePastConversations", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether the profile may recall the user's past conversations. Null uses the site default.")))
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
                ("ConnectionName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Provider connection name applied to generated profiles.")),
                ("ChatMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("TextInput", "AudioInput", "Conversation").Description("Chat mode applied to generated profiles.")),
                ("VoiceName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Speech synthesis voice applied to generated profiles.")),
                ("EnableTextToSpeechPlayback", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether generated profiles read their replies aloud.")),
                ("ChatDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Text deployment the generated profile talks to. It answers typed messages, including those typed during a voice conversation.")),
                ("ConversationDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Deployment that carries a spoken conversation for the generated profile. Naming one puts the profile in Conversation chat mode unless the template carries a chat mode of its own. Leave it empty to inherit the site's default realtime deployment.")),
                ("UtilityDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Utility deployment name used when the generated profile needs a utility or background model.")),
                ("OrchestratorName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Orchestrator name applied to generated profiles.")),
                ("RealtimeDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Legacy. Set ConversationDeploymentName instead. Still honored for existing templates: when set and no conversation deployment is named, it supplies one.")),
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
                ("A2AConnectionIds", BuildStringArraySchema("A2A connection identifiers automatically selected for generated profiles.")),
                ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Profile description copied into the generated profile.")),
                ("AgentAvailability", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("OnDemand", "AlwaysAvailable")
                    .Description("Agent availability applied when the generated profile type is Agent.")),
                ("AllowToolInvocation", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("When true and the generated profile type is Agent, the agent runs its own selected tools when another assistant calls it.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildSystemPromptTemplateMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known system-prompt template metadata stored when the template Source is SystemPrompt.")
            .Properties(
                ("SystemMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("System message contributed by this reusable system-prompt template.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAIPersonaMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The AI persona this resource links to, by the persona's technical name. Applied when the AI Personas feature is enabled.")
            .Properties(
                ("PersonaId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Technical name of the linked persona.")),
                ("Parameters", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("Liquid arguments used when rendering the persona instructions.").AdditionalProperties(true)))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildAIProfileSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known core profile settings.")
            .Properties(
                ("LockSystemMessage", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Prevents recipe or editor updates from changing the system message.")),
                ("IsListable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the profile is listed in the admin UI. Defaults to true.")),
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
                            ("IsUpdatable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether later session turns can update the extracted value.")),
                            ("FieldType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Text", "Number", "Boolean", "Date", "Enum", "List").Description("The type of the extracted value. Enum and List values are limited to AllowedValues.")),
                            ("AllowedValues", BuildStringArraySchema("The values an Enum or List field may take.")))
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
                ("ToolInstanceNames", BuildStringArraySchema("Tool instances available to post-session processing.")),
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
                                .Description("Predefined options used when Type is PredefinedOptions.")),
                            ("ToolNames", BuildStringArraySchema("Tools the task may call.")),
                            ("ToolInstanceNames", BuildStringArraySchema("Tool instances the task may call.")))
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
                ("ConversationDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Optional technical name of the speech-to-speech (realtime) deployment used in conversation mode. Leave empty to use the site default.")),
                ("VoiceName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional speech synthesis voice for conversation mode.")),
                ("EnableTextToSpeechPlayback", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether assistant responses should be played back with text-to-speech.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildDeploymentParametersMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Values of the model parameters the profile's deployments expose (for example reasoningEffort), keyed by the parameter's technical name.")
            .Properties(
                ("Values", BuildStringDictionarySchema("Parameter values sent with requests to the chat deployment.")),
                ("UtilityValues", BuildStringDictionarySchema("Parameter values sent with requests to the utility deployment.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildMcpMetadataSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Known MCP connection selections for this profile.")
            .Properties(
                ("ConnectionIds", BuildStringArraySchema("Selected connection identifiers.")),
                ("ToolNames", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object | SchemaValueType.Null)
                    .AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.Array | SchemaValueType.Null).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .Description("Per connection identifier, the names of the tools the profile may use from it. A connection with no entry, or a null entry, exposes every tool; an empty array exposes none.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildGuardrailSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Topic guardrails: what the assistant must not discuss, checked on the customer's messages, the replies, or both.")
            .Properties(
                ("CheckCustomer", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the customer's messages are checked against the topics.")),
                ("CheckReplies", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the assistant's replies are checked against the topics before they are sent.")),
                ("Topics", BuildStringArraySchema("Identifiers of the guardrail topics: System Prompt templates in the 'Guardrail topic' category.")),
                ("OtherTopics", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Other topics refused, described in a sentence or two, for this profile alone.")),
                ("PlaceholderReply", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the assistant says instead when a guardrail triggers. Empty uses a default.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildDataPrivacySettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("What the profile's conversations keep once they end.")
            .Properties(
                ("StorageMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Everything", "RedactPersonalData", "MetadataOnly").Description("Everything keeps the transcript; RedactPersonalData removes personal data from it; MetadataOnly keeps only the conversation's metadata.")),
                ("StorageModeSinceUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the storage mode last changed (ISO 8601, UTC). Conversations active after it get the mode. Set by the admin when the mode changes.")),
                ("RetentionDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(1).Description("Days without activity after which a conversation is deleted. Null keeps conversations.")),
                ("NamesToKeep", BuildStringArraySchema("Names that are not personal data and are kept when personal data is redacted, such as the assistant's, the business's and its staff's names.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildSpeakingStyleSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The speaking styles the assistant follows.")
            .Properties(
                ("Styles", BuildStringArraySchema("Identifiers of System Prompt templates in the 'Speaking style' category, for example speaking-style-echo-verification, speaking-style-nato-spelling, speaking-style-speech-normalization, speaking-style-fuzzy-name-matching, speaking-style-ai-disclosure, speaking-style-scope-limits, speaking-style-filler-words or speaking-style-high-empathy.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildSpeechSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How the assistant speaks and listens in voice conversations.")
            .Properties(
                ("SpeakingRate", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("How fast the profile speaks, as a multiple of the voice's normal pace (1 is normal), clamped to 0.25-4. Null uses the deployment's own pace.")),
                ("SpeakingEmotion", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Emotion the voice speaks with, as one word such as calm or excited, for providers with an emotion control (Cartesia). Others ignore it.")),
                ("PronunciationEntries", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Text", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The word or phrase to pronounce differently.")),
                            ("Alias", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("How it is spoken.")),
                            ("MatchCase", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether Text matches only with the same letter case.")))
                        .AdditionalProperties(true))
                    .Description("Pronunciation replacements applied before text is spoken.")),
                ("FallbackTextToSpeechDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Technical name of the text-to-speech deployment used when the primary one fails.")),
                ("BoostedKeywords", BuildStringArraySchema("Words the speech recognizer is biased toward, such as product names.")),
                ("RecognitionLanguages", BuildStringArraySchema("Languages the recognizer should expect, as BCP-47 tags. The first is the primary language.")),
                ("InterruptionSensitivity", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Minimum(0).Maximum(1).Description("How readily the caller's speech interrupts the assistant, from 0 (never interrupted) to 1 (any caller speech interrupts it). Null uses the default.")),
                ("EnableBackchanneling", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the assistant says short acknowledgements while the caller speaks.")),
                ("BackchannelPhrases", BuildStringArraySchema("The acknowledgements used for backchanneling. Empty uses a small built-in set.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildLanguageSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The languages the assistant may speak.")
            .Properties(
                ("AllowedLanguages", BuildStringArraySchema("Languages the profile may converse in, as BCP-47 tags. Empty means no restriction.")),
                ("StartingLanguage", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("BCP-47 tag of the language a conversation starts in. A conversation language set by the host (for example from a conversation variable) overrides it.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildFlowSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The conversation flow: a workflow whose AI conversation stages guide the conversation.")
            .Properties(
                ("WorkflowTypeId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the workflow type that starts with an AI Conversation Flow Started event and holds the stages. Empty turns the flow off.")),
                ("Flexible", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether every stage's instructions are given at once, so a caller who jumps between tasks is followed, rather than only the current stage's.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildContactMemorySettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The memory the assistant keeps about each CRM contact.")
            .Properties(
                ("UseMemory", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether what is remembered about the contact is given to the assistant.")),
                ("SaveMemory", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the memory is updated after each conversation.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildContactFieldMappingSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Copies extracted values onto the CRM contact when the conversation closes.")
            .Properties(
                ("Mappings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("FieldName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Name of the data extraction entry the value comes from.")),
                            ("ContactField", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The contact field the value is written to.")),
                            ("Mode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Overwrite", "FillIfEmpty", "Append").Description("Whether the value replaces the field, fills it only when empty, or is appended to it.")))
                        .AdditionalProperties(true))
                    .Description("The mappings, one per extracted value.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildPromptSecurityProfileSettingsSchema()
    {
        static JsonSchemaBuilder Tiers(string description)
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Array | SchemaValueType.Null)
                .Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Limit", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("How many are allowed in the window.")),
                        ("Window", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The window, as a time span such as 00:01:00.")))
                    .AdditionalProperties(true))
                .Description(description);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Per-profile overrides of the site's chat rate limits. A null member uses the site setting.")
            .Properties(
                ("MaxMessagesPerWindow", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description("Most messages a signed-in user may send per RateLimitWindow.")),
                ("RateLimitWindow", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The message rate limit window, as a time span such as 00:01:00.")),
                ("AnonymousMessageRateLimitTiers", Tiers("Message limits for anonymous visitors; every tier applies.")),
                ("AnonymousSessionStartRateLimitTiers", Tiers("Session start limits for anonymous visitors; every tier applies.")),
                ("MaxAnonymousSessionsPerWindow", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description("Most sessions an anonymous visitor may start per AnonymousSessionRateLimitWindow.")),
                ("AnonymousSessionRateLimitWindow", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The anonymous session window, as a time span such as 01:00:00.")))
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder BuildWebMcpProfileSettingsSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Whether the profile may use the tools the visitor's page offers through WebMCP.")
            .Properties(
                ("AllowPageTools", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the assistant may call the page's tools.")),
                ("AllowConsequentialPageTools", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the assistant may also call page tools that change something (consequential tools).")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder BuildStringDictionarySchema(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description(description);

    private static JsonSchemaBuilder BuildStringArraySchema(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description(description);
}
