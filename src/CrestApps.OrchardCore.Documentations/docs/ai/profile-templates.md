---
sidebar_label: AI Templates
sidebar_position: 10
slug: /ai/profile-templates
title: AI Templates
description: Reusable templates for creating AI Profiles or system prompts with pre-configured settings, parameters, tools, and data sources — managed via admin UI, markdown files, or App_Data directories.
---

| | |
| --- | --- |
| **Feature Name** | AI Templates |
| **Feature ID** | `CrestApps.OrchardCore.AI` |

## Overview

**AI Templates** provide reusable starting configurations for various AI use cases. Templates are **source-aware**, meaning each template belongs to a **source** that defines what the template is for. Out of the box, two sources are supported:

- **Profile** — Templates designed to create AI Profiles with pre-configured settings.
- **System Prompt** — Templates that provide reusable system prompt text.

Other modules can register additional template sources through the `AIOptions.AddTemplateSource()` API.

Templates can come from three discovery locations:

- **Database-stored templates** — Created and managed through the admin UI at runtime.
- **File-based templates (Modules)** — Discovered from `AITemplates/Profiles/` directories embedded in modules, using the same markdown front-matter format as [AI Prompt Templates](prompt-templates).
- **File-based templates (App_Data)** — Discovered from `App_Data/AITemplates/Profiles/` (global) and `App_Data/Sites/{tenantName}/AITemplates/Profiles/` (tenant-specific) directories on the filesystem.

Both file-based and database sources are merged by a unified service, with database templates taking precedence when names conflict.

For prompt-template selection specifically, runtime **System Prompt** templates created in the admin UI are merged with file-based prompt templates by technical name. Database-defined entries take precedence, and duplicate prompt IDs are removed from the picker.

### Key Benefits

- **Consistency** — Ensure new profiles start with approved configurations (system messages, parameters, tool selections).
- **Speed** — Skip repetitive configuration by applying a template and adjusting only what differs.
- **Multi-Source** — Define templates in code (markdown files shipped with modules), in the App_Data folder (for local customizations), or at runtime (admin UI).
- **Source-Aware** — Different template sources (Profile, System Prompt) show different editor fields, keeping the UI clean and focused.
- **Extensible** — Other modules can register their own template sources through `AIOptions.AddTemplateSource()`.
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

Navigate to **Artificial Intelligence → Templates** in the admin dashboard to create, edit, and delete database-stored templates.

### Creating a Template

1. Click **Add Template** to open the source selection modal.
2. Select the template source (e.g., **Profile** or **System Prompt**).
3. Fill in the template fields:
   - **Name** — A unique technical identifier (required).
   - **Display Text** — A human-readable title shown in the template dropdown.
   - **Description** — Optional description of what this template provides.
   - **Category** — Optional grouping category for organizing templates.
   - **Is Listable** — Whether this template appears in the selection dropdown (default: true).
4. For **Profile** source templates, configure the **Profile Settings** (stored as `ProfileTemplateMetadata` in `template.Properties`):
   - **Profile Type** — The type of profile to create (`Chat`, `Utility`, or `TemplatePrompt`). Required.
   - **Chat Deployment** — The AI deployment to use for chat completions (dropdown, grouped by connection, optional).
   - **Utility Deployment** — The AI deployment to use for auxiliary tasks (dropdown, grouped by connection, optional).
   - **Orchestrator Name** — The orchestrator to use (dropdown, defaults to "default").
   - **Welcome Message** — An initial greeting shown to users.
   - **Title Type** — How the session title is generated.
   - **Prompt Templates** — Add one or more reusable prompt templates from a searchable picker. Each selected template gets its own card, optional JSON parameters, can be removed independently, and can be reused multiple times.
   - **System Message** — The custom system instructions for the AI (supports Markdown with EasyMDE editor). Selected prompt templates are rendered before this field.
   - **Temperature** — Controls randomness (0.0 = deterministic, 1.0+ = creative).
   - **Top P** — Nucleus sampling threshold.
   - **Frequency Penalty** — Reduces repetition of frequent tokens.
   - **Presence Penalty** — Encourages topic diversity.
   - **Max Output Tokens** — Maximum tokens in the AI response.
   - **Past Messages Count** — Number of conversation history messages to include.
5. For **System Prompt** source templates, the editor shows only the **System Message** field (stored as `SystemPromptTemplateMetadata` in `template.Properties`). Profile-specific fields (connection, profile type, welcome message, model parameters, etc.) are hidden.
6. Configure **Capabilities** (Profile source only, when the relevant features are enabled):
   - **Tools** — Select which AI tools are available to the profile.
   - **MCP Connections** — Select which MCP connections are available.
7. Configure **Data Sources**:
   - **Data Source** — Select a data source for retrieval-augmented generation.
   - **Strictness**, **Top N Documents**, **Is In Scope**, **Filter** — RAG parameters.
8. Configure **Documents** (when the Documents feature is enabled):
   - **Allow Session Documents** — Whether users can upload documents during chat sessions.
   - **Profile Documents** — Upload documents directly to the template. Documents are processed (text extraction, chunking, and embedding generation) and stored with the template. When the template is applied to a new profile, all attached documents — including their text chunks and embeddings — are cloned to the new profile automatically.
     - **Top N** — Number of top matching document chunks to include in AI context (default: 3).
9. Configure **Data Processing & Metrics** (when the Chat and Analytics features are enabled):
   - **Session Settings** — Session inactivity timeout, AI resolution detection.
   - **Data Extraction** — Enable data extraction with extraction entries.
   - **Post-Session Processing** — Configure post-session tasks.
   - **Analytics** — Enable session/conversion metrics and define conversion goals.
10. Click **Save** to store the template.

:::note
The template editor mirrors the AI Profile editor with the same tabbed layout (Capabilities, Documents, Data Processing & Metrics). External module features (tools, MCP connections, data sources, documents, analytics) add their own tabs and sections to the template editor when their features are enabled.
:::

### Editing and Deleting Templates

- Click a template name in the list to edit it.
- Use the delete action to remove a database template.
- File-based templates (read-only) cannot be edited or deleted through the UI.

---

## Defining Templates via Markdown Files

### Module-Embedded Templates

Create `.md` files in the `AITemplates/Profiles/` directory of any module:

```
MyModule/
└── AITemplates/
    └── Profiles/
        ├── customer-support.md
        └── content-writer.md
```

The filename (without extension) becomes the template name. For example, `customer-support.md` registers a template with the name `customer-support`.

### App_Data Templates

You can also place template files in the `App_Data` directory for local customization without modifying module code. Two locations are scanned:

- **Global** — `App_Data/AITemplates/Profiles/` — Templates available to all tenants.
- **Tenant-specific** — `App_Data/Sites/{tenantName}/AITemplates/Profiles/` — Templates specific to a tenant.

Templates in App_Data use the same markdown front-matter format as module-embedded templates. The `Source` front-matter field can be used to override the default source (which is `Profile`).

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
| `Source` | string | `Profile` | The template source (e.g., `Profile` or `SystemPrompt`). Overrides the default. |
| `IsListable` | bool | `true` | Whether this template appears in selection dropdowns |
| `ProfileType` | string | `null` | Profile type: `Chat`, `Utility`, or `TemplatePrompt` |
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

Any additional `Key: Value` pairs are stored in the template's `Properties` for custom use.

:::note
When templates are loaded from markdown files, profile-specific front-matter fields (e.g., `ProfileType`, `Temperature`, `SystemMessage`) are automatically mapped into the appropriate metadata object (`ProfileTemplateMetadata` or `SystemPromptTemplateMetadata`) in the template's `Properties`. You do not need to manually structure the front matter into nested metadata objects.
:::

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

### IAIProfileTemplateManager

The `IAIProfileTemplateManager` interface extends `INamedSourceCatalogManager<AIProfileTemplate>` and provides full CRUD operations plus unified access to all templates (both database and file-based). It merges file-based templates from `IAIProfileTemplateProvider` implementations with database-stored templates, with database entries taking precedence.

```csharp
public interface IAIProfileTemplateManager : INamedSourceCatalogManager<AIProfileTemplate>
{
    /// <summary>
    /// Gets only templates marked as listable.
    /// </summary>
    ValueTask<IEnumerable<AIProfileTemplate>> GetListableAsync();
}
```

#### Managing Templates

Inject `IAIProfileTemplateManager` for full CRUD and query operations:

```csharp
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using OrchardCore.Entities;

public sealed class MyService
{
    private readonly IAIProfileTemplateManager _templateManager;

    public MyService(IAIProfileTemplateManager templateManager)
    {
        _templateManager = templateManager;
    }

    public async Task CreateTemplateAsync()
    {
        // Create a new template instance with a source.
        var template = await _templateManager.NewAsync("Profile");
        template.Name = "my-template";
        template.DisplayText = "My Custom Template";
        template.Description = "A reusable starting point for customer service profiles";
        template.Category = "Customer Service";

        // Source-specific fields are stored as metadata in Properties.
        var metadata = template.As<ProfileTemplateMetadata>();
        metadata.ProfileType = AIProfileType.Chat;
        metadata.Temperature = 0.7f;
        metadata.SystemMessage = "You are a helpful assistant.";
        template.Put(metadata);

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
            var metadata = template.As<ProfileTemplateMetadata>();
            metadata.Temperature = 0.9f;
            template.Put(metadata);
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

    public async Task QueryBySourceAsync()
    {
        // Get only Profile-source templates (includes file-based).
        var profileTemplates = await _templateManager.GetAsync("Profile");

        // Get only listable templates from all sources.
        var listableTemplates = await _templateManager.GetListableAsync();

        // Find a template by name and source.
        var template = await _templateManager.FindAsync("my-template", "Profile");
    }
}
```

:::tip
`IAIProfileTemplateManager` automatically merges database-stored and file-based templates. All query methods (`GetAllAsync`, `GetAsync`, `FindByIdAsync`, etc.) return results from both sources, with database entries taking precedence when names conflict.
:::

---

## Deployment

AI Profile Templates can be exported and imported using Orchard Core's deployment infrastructure. This requires the `OrchardCore.Deployment` feature to be enabled.

### Adding a Deployment Step

1. Navigate to **Configuration → Import/Export → Deployment Plans**.
2. Create or edit a deployment plan.
3. Add the **AI Profile Templates** step.
4. Choose **Include all** to export all templates, or select specific templates by name.

The deployment step exports database-stored templates as JSON. File-based templates (from module `AITemplates/Profiles/` directories or App_Data folders) are not included in deployments since they are shipped with module code or managed locally.

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
          "Source": "Profile",
          "IsListable": true,
          "Properties": {
            "ProfileTemplateMetadata": {
              "ProfileType": "Chat",
              "ChatDeploymentId": "customer-support-chat",
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
          }
        }
      ]
    }
  ]
}
```

For **System Prompt** source templates, use `SystemPromptTemplateMetadata` instead:

```json
{
  "steps": [
    {
      "name": "AIProfileTemplate",
      "templates": [
        {
          "Name": "my-system-prompt",
          "DisplayText": "My Reusable System Prompt",
          "Description": "A reusable system prompt for various AI profiles",
          "Category": "Prompts",
          "Source": "SystemPrompt",
          "IsListable": true,
          "Properties": {
            "SystemPromptTemplateMetadata": {
              "SystemMessage": "You are a helpful, accurate, and concise assistant."
            }
          }
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

#### Generic Fields (all sources)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | string | Yes | Unique technical name for the template |
| `Source` | string | No | Template source (e.g., `Profile` or `SystemPrompt`). Defaults to `Profile`. |
| `DisplayText` | string | No | Human-readable title |
| `Description` | string | No | Template description |
| `Category` | string | No | Grouping category |
| `IsListable` | bool | No | Whether the template appears in dropdowns (default: `true`) |
| `Properties` | object | No | Source-specific metadata and external module settings |

#### ProfileTemplateMetadata Fields (Profile source)

These fields are nested inside `Properties.ProfileTemplateMetadata`:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `ProfileType` | string | `null` | `Chat`, `Utility`, or `TemplatePrompt` |
| `SystemMessage` | string | `null` | System prompt text |
| `WelcomeMessage` | string | `null` | Initial greeting for chat profiles |
| `TitleType` | string | `null` | `Generated`, `Fixed`, or `None` |
| `OrchestratorName` | string | `null` | Orchestrator to use |
| `PromptTemplate` | string | `null` | Prompt template (for `TemplatePrompt` type) |
| `PromptSubject` | string | `null` | Prompt subject |
| `Temperature` | float | `null` | Model temperature (0.0–2.0) |
| `TopP` | float | `null` | Nucleus sampling threshold (0.0–1.0) |
| `FrequencyPenalty` | float | `null` | Frequency penalty (-2.0–2.0) |
| `PresencePenalty` | float | `null` | Presence penalty (-2.0–2.0) |
| `MaxOutputTokens` | int | `null` | Maximum output tokens |
| `PastMessagesCount` | int | `null` | Past conversation messages to include |
| `ToolNames` | string[] | `null` | Array of AI tool names |

#### SystemPromptTemplateMetadata Fields (SystemPrompt source)

These fields are nested inside `Properties.SystemPromptTemplateMetadata`:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `SystemMessage` | string | `null` | The reusable system prompt text |

---

## How Template Values Map to AI Profile Fields

When a **Profile** source template is applied, the `ProfileTemplateMetadata` fields are mapped to the new AI Profile:

### Core Profile Fields

| Template Metadata Field | AI Profile Field | Notes |
|------------------------|-----------------|-------|
| `ProfileType` | `Type` | The profile type (Chat, Utility, TemplatePrompt) |
| `ChatDeploymentId` | `ChatDeploymentId` | The selected chat deployment, which also determines the provider connection |
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
5. If your driver is source-specific, use `.RenderWhen()` to only show fields for the appropriate source.

```csharp
using CrestApps.OrchardCore.AI.Core;
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
        }).Location("Content:10#Capabilities;5")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

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

When a template is applied to a new profile, all `template.Properties` entries (except `ProfileTemplateMetadata` and `SystemPromptTemplateMetadata`, which are template-specific) are automatically copied to both `profile.Properties` and `profile.Settings`, so your custom settings will be available to both `profile.As<T>()` and `profile.GetSettings<T>()` on the profile side.

---

## Registering Custom Template Sources

Other modules can register their own template sources to extend the Templates UI. Each source appears as a card in the source selection modal when creating a new template.

### Using the Extension Method

In your module's `Startup.cs`:

```csharp
using CrestApps.OrchardCore.AI.Core;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITemplateSource("MyCustomSource", entry =>
        {
            entry.DisplayName = new LocalizedString("MyCustomSource", "My Custom Source");
            entry.Description = new LocalizedString("MyCustomSource", "Templates for my custom use case.");
        });
    }
}
```

### Using AIOptions Directly

```csharp
services.Configure<AIOptions>(o =>
{
    o.AddTemplateSource("MyCustomSource", entry =>
    {
        entry.DisplayName = new LocalizedString("MyCustomSource", "My Custom Source");
        entry.Description = new LocalizedString("MyCustomSource", "Templates for my custom use case.");
    });
});
```

### Source-Aware Display Drivers

When creating a display driver for `AIProfileTemplate`, you can use `template.Source` to conditionally render fields based on the template source. Use `.RenderWhen()` to hide sections that don't apply:

```csharp
return Initialize<MyViewModel>("MySection_Edit", model =>
{
    // ...
}).Location("Content:5")
.RenderWhen(() => Task.FromResult(template.Source == "MyCustomSource"));
```

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
