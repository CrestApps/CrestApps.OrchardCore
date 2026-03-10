---
sidebar_label: AI Profile Templates
sidebar_position: 10
slug: /ai/profile-templates
title: AI Profile Templates
description: Reusable templates for creating AI Profiles with pre-configured settings, parameters, tools, and data sources — managed via admin UI or markdown files.
---

| | |
| --- | --- |
| **Feature Name** | AI Profile Templates |
| **Feature ID** | `CrestApps.OrchardCore.AI` |

## Overview

**AI Profile Templates** provide reusable starting configurations for creating new AI Profiles. Instead of configuring every profile from scratch, templates let you define common combinations of system messages, model parameters, tool selections, and other settings once — then apply them as a starting point when creating new profiles.

Templates can come from two sources:

- **Database-stored templates** — Created and managed through the admin UI at runtime.
- **File-based templates** — Discovered from `AITemplates/Profiles/` directories in modules, using the same markdown front-matter format as [AI Prompt Templates](prompt-templates).

Both sources are merged by a unified service, with database templates taking precedence when names conflict.

### Key Benefits

- **Consistency** — Ensure new profiles start with approved configurations (system messages, parameters, tool selections).
- **Speed** — Skip repetitive configuration by applying a template and adjusting only what differs.
- **Dual-Source** — Define templates in code (markdown files shipped with modules) or at runtime (admin UI).
- **Non-Destructive** — Applying a template pre-fills the form; users can modify any value before saving.

---

## Using Profile Templates

When creating a new AI Profile, a **Template** dropdown appears at the top of the create form. Select a template and click **Apply** to pre-fill the profile fields with the template's values. You can then adjust any field before saving.

The Apply action redirects to the create page with the selected template's values pre-populated. All display drivers (including those from tool, data source, chat, MCP, analytics, and documents modules) automatically render the pre-filled values.

When a template includes attached documents (uploaded via the Profile Documents feature), applying the template **clones** all documents — including their extracted text chunks and pre-computed embeddings — to the new profile. This means the new profile immediately has the same RAG (Retrieval Augmented Generation) knowledge base as the template, without needing to re-upload or re-process the files.

:::tip
Templates only apply to the **create** form. Existing profiles are not affected by template changes.
:::

---

## Managing Templates via Admin UI

Navigate to **Artificial Intelligence → Profile Templates** in the admin dashboard to create, edit, and delete database-stored templates.

### Creating a Template

1. Click **Add Template** to open the template editor.
2. Fill in the template fields:
   - **Name** — A unique technical identifier (required).
   - **Display Text** — A human-readable title shown in the template dropdown.
   - **Description** — Optional description of what this template provides.
   - **Category** — Optional grouping category for organizing templates.
   - **Is Listable** — Whether this template appears in the selection dropdown (default: true).
3. Configure the **Profile Settings**:
   - **Profile Type** — The type of profile to create (`Chat`, `Utility`, or `TemplatePrompt`). Required.
   - **Chat Deployment** — The AI deployment to use for chat completions (dropdown, grouped by connection, optional).
   - **Utility Deployment** — The AI deployment to use for auxiliary tasks (dropdown, grouped by connection, optional).
   - **Orchestrator Name** — The orchestrator to use (dropdown, defaults to "default").
   - **Welcome Message** — An initial greeting shown to users.
   - **Title Type** — How the session title is generated.
4. Set **Model Parameters**:
   - **System Message** — The system prompt for the AI (supports Markdown with EasyMDE editor).
   - **Temperature** — Controls randomness (0.0 = deterministic, 1.0+ = creative).
   - **Top P** — Nucleus sampling threshold.
   - **Frequency Penalty** — Reduces repetition of frequent tokens.
   - **Presence Penalty** — Encourages topic diversity.
   - **Max Output Tokens** — Maximum tokens in the AI response.
   - **Past Messages Count** — Number of conversation history messages to include.
5. Configure **Capabilities** (when the relevant features are enabled):
   - **Tools** — Select which AI tools are available to the profile.
   - **MCP Connections** — Select which MCP connections are available.
6. Configure **Data Sources**:
   - **Data Source** — Select a data source for retrieval-augmented generation.
   - **Strictness**, **Top N Documents**, **Is In Scope**, **Filter** — RAG parameters.
7. Configure **Documents** (when the Documents feature is enabled):
   - **Allow Session Documents** — Whether users can upload documents during chat sessions.
   - **Profile Documents** — Upload documents directly to the template. Documents are processed (text extraction, chunking, and embedding generation) and stored with the template. When the template is applied to a new profile, all attached documents — including their text chunks and embeddings — are cloned to the new profile automatically.
     - **Top N** — Number of top matching document chunks to include in AI context (default: 3).
8. Configure **Data Processing & Metrics** (when the Chat and Analytics features are enabled):
   - **Session Settings** — Session inactivity timeout, AI resolution detection.
   - **Data Extraction** — Enable data extraction with extraction entries.
   - **Post-Session Processing** — Configure post-session tasks.
   - **Analytics** — Enable session/conversion metrics and define conversion goals.
9. Click **Save** to store the template.

:::note
The template editor mirrors the AI Profile editor with the same tabbed layout (Capabilities, Documents, Data Processing & Metrics). External module features (tools, MCP connections, data sources, documents, analytics) add their own tabs and sections to the template editor when their features are enabled.
:::

### Editing and Deleting Templates

- Click a template name in the list to edit it.
- Use the delete action to remove a database template.
- File-based templates (read-only) cannot be edited or deleted through the UI.

---

## Defining Templates via Markdown Files

Create `.md` files in the `AITemplates/Profiles/` directory of any module:

```
MyModule/
└── AITemplates/
    └── Profiles/
        ├── customer-support.md
        └── content-writer.md
```

The filename (without extension) becomes the template name. For example, `customer-support.md` registers a template with the name `customer-support`.

### Front Matter Format

Use YAML-style front matter to define template metadata and profile settings:

```markdown
---
Title: Customer Support Bot
Description: Template for customer support chatbots
Category: Customer Service
IsListable: true
ProfileType: Chat
ChatDeploymentId: your-chat-deployment-id
UtilityDeploymentId: your-utility-deployment-id
WelcomeMessage: Hello! How can I help you today?
TitleType: Generated
Temperature: 0.7
TopP: 0.9
FrequencyPenalty: 0.0
PresencePenalty: 0.0
MaxTokens: 800
PastMessagesCount: 10
ToolNames: web-search, knowledge-base
---

You are a professional customer support agent.
Your goal is to help customers resolve issues efficiently and courteously.
Always be helpful, accurate, and empathetic.
```

The body after the front matter becomes the **System Message**.

### Supported Front Matter Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Title` | string | Derived from filename | Display title shown in the template dropdown |
| `Description` | string | `null` | Description of what this template provides |
| `Category` | string | `null` | Category for grouping templates |
| `IsListable` | bool | `true` | Whether this template appears in selection dropdowns |
| `ProfileType` | string | `null` | Profile type: `Chat`, `Utility`, or `TemplatePrompt` |
| `ConnectionName` | string | `null` | AI provider connection name (derived from deployment if not set) |
| `ChatDeploymentId` | string | `null` | Deployment ID for chat completions |
| `UtilityDeploymentId` | string | `null` | Deployment ID for auxiliary/utility tasks |
| `WelcomeMessage` | string | `null` | Initial greeting shown to users |
| `TitleType` | string | `null` | Session title type: `Generated`, `Fixed`, or `None` |
| `OrchestratorName` | string | `null` | Name of the orchestrator to use |
| `PromptTemplate` | string | `null` | Template for the prompt (for `TemplatePrompt` type) |
| `PromptSubject` | string | `null` | Subject of the prompt |
| `Temperature` | float | `null` | Model temperature (0.0–2.0) |
| `TopP` | float | `null` | Nucleus sampling threshold (0.0–1.0) |
| `FrequencyPenalty` | float | `null` | Frequency penalty (-2.0–2.0) |
| `PresencePenalty` | float | `null` | Presence penalty (-2.0–2.0) |
| `MaxTokens` | int | `null` | Maximum output tokens |
| `MaxOutputTokens` | int | `null` | Alias for `MaxTokens` |
| `PastMessagesCount` | int | `null` | Number of past messages to include |
| `ToolNames` | string | `null` | Comma-separated list of tool names |

Any additional `Key: Value` pairs are stored in the template's `AdditionalProperties` for custom use.

### Example: Content Writer Template

```markdown
---
Title: Content Writer
Description: Template for AI-assisted content creation profiles
Category: Content
IsListable: true
ProfileType: Chat
Temperature: 0.8
TopP: 0.95
MaxTokens: 2000
PastMessagesCount: 5
---

You are an expert content writer.
Help users create engaging, well-structured content for blogs, articles, and marketing materials.
Maintain a professional yet approachable tone.
```

---

## Permissions

| Permission | Description |
|------------|-------------|
| `ManageAIProfileTemplates` | Allows creating, editing, and deleting AI profile templates |

This permission is required to access the Profile Templates admin page and to manage database-stored templates.

---

## Programmatic Access

### IAIProfileTemplateService

The `IAIProfileTemplateService` interface provides unified read-only access to all templates (both database and file-based):

```csharp
public interface IAIProfileTemplateService
{
    /// <summary>
    /// Gets all available templates from all sources.
    /// </summary>
    Task<IEnumerable<AIProfileTemplate>> GetAllAsync();

    /// <summary>
    /// Gets only templates marked as listable.
    /// </summary>
    Task<IEnumerable<AIProfileTemplate>> GetListableAsync();

    /// <summary>
    /// Finds a template by its unique identifier.
    /// </summary>
    Task<AIProfileTemplate> FindByIdAsync(string itemId);
}
```

### Managing Templates with INamedCatalogManager

For full CRUD operations on database-stored templates, inject `INamedCatalogManager<AIProfileTemplate>`:

```csharp
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

public sealed class MyService
{
    private readonly INamedCatalogManager<AIProfileTemplate> _templateManager;

    public MyService(INamedCatalogManager<AIProfileTemplate> templateManager)
    {
        _templateManager = templateManager;
    }

    public async Task CreateTemplateAsync()
    {
        // Create a new template instance.
        var template = await _templateManager.NewAsync();
        template.Name = "my-template";
        template.DisplayText = "My Custom Template";
        template.Description = "A reusable starting point for customer service profiles";
        template.Category = "Customer Service";
        template.ProfileType = AIProfileType.Chat;
        template.Temperature = 0.7f;
        template.SystemMessage = "You are a helpful assistant.";

        // Validate before saving.
        var result = await _templateManager.ValidateAsync(template);

        if (result.Succeeded)
        {
            await _templateManager.CreateAsync(template);
        }
    }

    public async Task UpdateTemplateAsync(string templateId)
    {
        var template = await _templateManager.FindByIdAsync(templateId);

        if (template is not null)
        {
            template.Temperature = 0.9f;
            await _templateManager.UpdateAsync(template);
        }
    }

    public async Task DeleteTemplateAsync(string templateId)
    {
        var template = await _templateManager.FindByIdAsync(templateId);

        if (template is not null)
        {
            await _templateManager.DeleteAsync(template);
        }
    }
}
```

### Querying Templates with INamedCatalog

For read-only access to database-stored templates, inject `INamedCatalog<AIProfileTemplate>`:

```csharp
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

public sealed class MyQueryService
{
    private readonly INamedCatalog<AIProfileTemplate> _catalog;

    public MyQueryService(INamedCatalog<AIProfileTemplate> catalog)
    {
        _catalog = catalog;
    }

    public async Task<AIProfileTemplate> GetByNameAsync(string name)
    {
        return await _catalog.FindByNameAsync(name);
    }

    public async Task<IEnumerable<AIProfileTemplate>> GetAllAsync()
    {
        return await _catalog.GetAllAsync();
    }
}
```

:::tip
Use `IAIProfileTemplateService` when you need templates from **all sources** (database + file-based). Use `INamedCatalog<AIProfileTemplate>` or `INamedCatalogManager<AIProfileTemplate>` when you only need database-stored templates.
:::

---

## Deployment

AI Profile Templates can be exported and imported using Orchard Core's deployment infrastructure. This requires the `OrchardCore.Deployment` feature to be enabled.

### Adding a Deployment Step

1. Navigate to **Configuration → Import/Export → Deployment Plans**.
2. Create or edit a deployment plan.
3. Add the **AI Profile Templates** step.
4. Choose **Include all** to export all templates, or select specific templates by name.

The deployment step exports database-stored templates as JSON. File-based templates (from module `AITemplates/Profiles/` directories) are not included in deployments since they are shipped with module code.

---

## Recipes

AI Profile Templates support Orchard Core recipes for importing and exporting template configurations. This requires the `OrchardCore.Recipes.Core` feature.

### Recipe Step Format

Use the `AIProfileTemplate` step key to define templates in a recipe:

```json
{
  "steps": [
    {
      "name": "AIProfileTemplate",
      "templates": [
        {
          "Name": "customer-support",
          "DisplayText": "Customer Support Bot",
          "Description": "Template for customer support chatbots",
          "Category": "Customer Service",
          "IsListable": true,
          "ProfileType": "Chat",
          "ConnectionName": "openai-main",
          "ChatDeploymentId": "your-chat-deployment-id",
          "UtilityDeploymentId": "your-utility-deployment-id",
          "SystemMessage": "You are a professional customer support agent.",
          "WelcomeMessage": "Hello! How can I help you today?",
          "TitleType": "Generated",
          "Temperature": 0.7,
          "TopP": 0.9,
          "FrequencyPenalty": 0.0,
          "PresencePenalty": 0.0,
          "MaxOutputTokens": 800,
          "PastMessagesCount": 10,
          "ToolNames": ["web-search", "knowledge-base"]
        }
      ]
    }
  ]
}
```

### Recipe Behavior

- If a template with the same `ItemId` exists, it is updated.
- If no match by `ItemId`, the recipe searches by `Name` and updates if found.
- If no existing template matches, a new one is created.
- Validation runs before each template is saved; errors are reported per-template without stopping the recipe.

### Recipe Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | string | Yes | Unique technical name for the template |
| `DisplayText` | string | No | Human-readable title |
| `Description` | string | No | Template description |
| `Category` | string | No | Grouping category |
| `IsListable` | bool | No | Whether the template appears in dropdowns (default: `true`) |
| `ProfileType` | string | No | `Chat`, `Utility`, or `TemplatePrompt` |
| `ConnectionName` | string | No | AI provider connection name (derived from deployment if not set) |
| `ChatDeploymentId` | string | No | Deployment ID for chat completions |
| `UtilityDeploymentId` | string | No | Deployment ID for auxiliary/utility tasks |
| `SystemMessage` | string | No | System prompt text |
| `WelcomeMessage` | string | No | Initial greeting for chat profiles |
| `TitleType` | string | No | `Generated`, `Fixed`, or `None` |
| `OrchestratorName` | string | No | Orchestrator to use |
| `PromptTemplate` | string | No | Prompt template (for `TemplatePrompt` type) |
| `PromptSubject` | string | No | Prompt subject |
| `Temperature` | float | No | Model temperature (0.0–2.0) |
| `TopP` | float | No | Nucleus sampling threshold (0.0–1.0) |
| `FrequencyPenalty` | float | No | Frequency penalty (-2.0–2.0) |
| `PresencePenalty` | float | No | Presence penalty (-2.0–2.0) |
| `MaxOutputTokens` | int | No | Maximum output tokens |
| `PastMessagesCount` | int | No | Past conversation messages to include |
| `ToolNames` | string[] | No | Array of AI tool names |

---

## How Template Values Map to AI Profile Fields

When a template is applied, the following fields are pre-filled on the new AI Profile:

### Core Profile Fields

| Template Field | AI Profile Field | Notes |
|----------------|-----------------|-------|
| `ProfileType` | `Type` | The profile type (Chat, Utility, TemplatePrompt) |
| `ConnectionName` | `ConnectionName` | AI provider connection (derived from deployment if not set) |
| `ChatDeploymentId` | `ChatDeploymentId` | Deployment for chat completions |
| `UtilityDeploymentId` | `UtilityDeploymentId` | Deployment for auxiliary tasks |
| `OrchestratorName` | `OrchestratorName` | The orchestrator to use |
| `SystemMessage` | `AIProfileMetadata.SystemMessage` | Via profile metadata |
| `WelcomeMessage` | `WelcomeMessage` | Only for Chat profiles |
| `TitleType` | `TitleType` | Only for Chat profiles |
| `PromptTemplate` | `PromptTemplate` | Only for TemplatePrompt profiles |
| `PromptSubject` | `PromptSubject` | Only for TemplatePrompt profiles |
| `Temperature` | `AIProfileMetadata.Temperature` | Model parameter |
| `TopP` | `AIProfileMetadata.TopP` | Model parameter |
| `FrequencyPenalty` | `AIProfileMetadata.FrequencyPenalty` | Model parameter |
| `PresencePenalty` | `AIProfileMetadata.PresencePenalty` | Model parameter |
| `MaxOutputTokens` | `AIProfileMetadata.MaxTokens` | Model parameter |
| `PastMessagesCount` | `AIProfileMetadata.PastMessagesCount` | Model parameter |

### External Module Settings

Settings stored in `template.Properties` by external module drivers are automatically copied to the new profile. This includes:

| Module | Settings Applied | Description |
|--------|-----------------|-------------|
| **AI (Tools)** | `FunctionInvocationMetadata` | Selected tool names |
| **AI Chat** | `AIChatProfileSettings` | Admin menu visibility |
| **AI Chat** | `AIProfileDataExtractionSettings` | Data extraction entries, session timeout |
| **AI Chat** | `AIProfilePostSessionSettings` | Post-session tasks and tools |
| **AI Chat (Analytics)** | `AnalyticsMetadata` | Session metrics, conversion goals, AI resolution detection |
| **AI Documents** | `DocumentsMetadata` | Attached documents with text chunks and embeddings (cloned to new profile) |
| **AI Documents** | `AIProfileSessionDocumentsMetadata` | Allow session documents toggle |
| **AI MCP** | `AIProfileMcpMetadata` | MCP connection selections |
| **AI DataSources** | `DataSourceMetadata`, `AIDataSourceRagMetadata` | Data source, strictness, top N, filters |

:::note
Template application pre-fills the form — users can modify any value before saving the profile.
:::

---

## Extending Templates with Custom Display Drivers

If you have a custom module that adds a `DisplayDriver<AIProfile>`, you can add a corresponding `DisplayDriver<AIProfileTemplate>` to support templates for your module's settings. The template driver should:

1. Extend `DisplayDriver<AIProfileTemplate>` instead of `DisplayDriver<AIProfile>`.
2. Use `template.As<T>()` and `template.Put<T>()` (from `OrchardCore.Entities`) to read/write settings in `template.Properties`.
3. Reuse the same ViewModel and shape name as the profile driver, so the same Razor view is rendered.
4. Use the same `.Location()` positioning as the profile driver.

```csharp
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

public sealed class AIProfileTemplateMySettingsDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<MySettingsViewModel>("MySettings_Edit", model =>
        {
            var settings = template.As<MySettings>();
            model.MySetting = settings.MySetting;
        }).Location("Content:5#Capabilities:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        var model = new MySettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = template.As<MySettings>();
        settings.MySetting = model.MySetting;
        template.Put(settings);

        return Edit(template, context);
    }
}
```

Register the driver in your module's `Startup.cs`:

```csharp
services.AddDisplayDriver<AIProfileTemplate, AIProfileTemplateMySettingsDisplayDriver>();
```

When a template is applied to a new profile, all `template.Properties` entries are automatically copied to both `profile.Properties` and `profile.Settings`, so your custom settings will be available to both `profile.As<T>()` and `profile.GetSettings<T>()` on the profile side.

---

## Built-in Templates

The AI module ships with a built-in profile template:

### Chat Session Summarizer

A **TemplatePrompt** profile template that summarizes chat sessions. When applied to a new profile, it creates a "tool" that can be invoked during a chat session to produce a concise summary of the conversation.

- **Type**: TemplatePrompt
- **Category**: Productivity
- **Prompt Subject**: Summary
- **Temperature**: 0.3

The template uses a Liquid `PromptTemplate` that iterates over the session messages and sends them to the AI with instructions to produce a structured summary including key topics, decisions, and action items.

To use this template:
1. Navigate to **AI Services → AI Profiles → Create**.
2. Select **Chat Session Summarizer** from the template dropdown.
3. Click **Apply** to pre-fill the profile fields.
4. Adjust settings if needed and save.

---

## Recipe Step Schemas

When the `CrestApps.OrchardCore.Recipes` feature is enabled, JSON schemas are available for all AI-related recipe steps. These schemas provide validation and documentation for recipe authoring.

| Recipe Step | Schema Name | Description |
|-------------|-------------|-------------|
| `AIProfile` | `AIProfileRecipeStep` | Creates or updates AI profiles |
| `AIProfileTemplate` | `AIProfileTemplateRecipeStep` | Creates or updates AI profile templates |
| `AIDeployment` | `AIDeploymentRecipeStep` | Creates or updates AI model deployments |
| `DeleteAIDeployments` | `DeleteAIDeploymentsRecipeStep` | Deletes AI deployments by name or all |
| `AIProviderConnections` | `AIProviderConnectionsRecipeStep` | Creates or updates AI provider connections |
