---
sidebar_label: AI Templates
sidebar_position: 15
slug: /ai/ai-prompt-templates
title: AI Templates
description: Centralized management for AI system prompts with Liquid template rendering, extensible parsers, file-based discovery, and reusable prompt composition.
---

| | |
| --- | --- |
| **Feature Name** | AI Prompt Templates |
| **Feature ID** | `CrestApps.OrchardCore.AI.Prompting` |

## Overview

The **AI Templates** module provides a centralized system for managing AI system prompts. Instead of scattering hardcoded prompt strings across your codebase, you define prompts as reusable `.md` files with metadata and Liquid template support.

This module is built on top of the standalone **CrestApps.AI.Prompting** library, which can be used in any .NET project — not just Orchard Core.

### Key Benefits

- **Centralized Management** — All prompts live in dedicated `AITemplates/Prompts/` directories, easy to find and maintain.
- **Liquid Templates** — Use Liquid syntax (via [Fluid](https://github.com/sebastienros/fluid)) for dynamic prompt generation with variables, conditionals, and loops.
- **Metadata** — Add title, description, category, and custom properties to each prompt via front matter.
- **JSON Compaction** — Fenced ` ```json ``` ` blocks are automatically compacted during parsing to reduce token usage while keeping source files readable.
- **Caching** — Parsed templates are cached in memory and invalidated when the tenant shell is released or the application restarts.
- **Composition** — Merge multiple prompts together, use the `AITemplateBuilder` for efficient assembly, or include one prompt inside another using the `include_prompt` filter.
- **Feature-Aware** — In Orchard Core, prompts are automatically tied to module features and only available when those features are enabled.
- **Extensible Parsers** — Markdown front matter parsing ships by default; add YAML, JSON, or other formats by implementing `IAITemplateParser`.
- **Extensible** — Register prompts via code, files, or custom providers.

---

## Defining Prompt Templates

### File-Based Prompts

Create a `.md` file in the `AITemplates/Prompts/` directory of any module or project:

```
MyModule/
└── AITemplates/
    └── Prompts/
        ├── my-prompt.md           # Module-level prompt
        └── MyModule.FeatureId/
            └── feature-prompt.md  # Feature-specific prompt
```

The filename (without extension) becomes the prompt ID. For example, `my-prompt.md` registers a prompt with ID `my-prompt`.

### Front Matter Metadata

Add YAML-style front matter at the top of the file to provide metadata. A blank line after the closing `---` is recommended for readability:

```markdown
---
Title: Generate Chart Configuration
Description: Instructs AI to produce Chart.js JSON configuration
IsListable: false
Category: Data Visualization
CustomKey: CustomValue
---

You are a data visualization expert. Generate a Chart.js configuration...
```

#### Supported Metadata Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Title` | string | Derived from filename | Display title for UI |
| `Description` | string | `null` | Description shown in prompt selection UI. For templates with parameters, describe the available parameters here. |
| `IsListable` | bool | `true` | Whether this prompt appears in selection dropdowns |
| `Category` | string | `null` | Category for grouping prompts in the UI |

Any additional `Key: Value` pairs are stored in `AdditionalProperties` for custom use.

### JSON Compaction

Fenced ` ```json ``` ` code blocks in template files are automatically compacted during parsing. This lets you write readable, pretty-printed JSON in your source files while keeping the actual system prompt token-efficient at runtime:

**Source file:**

````markdown
[Output Format]
```json
{
    "type": "bar",
    "data": {
        "labels": ["Jan", "Feb", "Mar"]
    }
}
```
````

**Parsed output:**

````markdown
[Output Format]
```json
{"type":"bar","data":{"labels":["Jan","Feb","Mar"]}}
```
````

Non-JSON fenced blocks and invalid JSON are left unchanged.

### Code-Based Registration

Register prompts programmatically via `AITemplateOptions`:

```csharp
services.Configure<AITemplateOptions>(options =>
{
    options.Templates.Add(new AITemplate
    {
        Id = "my-code-prompt",
        Content = "You are a helpful {{ role }} assistant.",
        Metadata = new AITemplateMetadata
        {
            Title = "Code-Registered Prompt",
            Description = "Registered via C# code. Parameters - role (string): the assistant role.",
            Category = "General",
        },
    });
});
```

### Embedded Resource Registration

For class libraries (non-module projects), embed prompt `.md` files as assembly resources and register them:

```xml
<!-- In your .csproj -->
<ItemGroup>
  <EmbeddedResource Include="AITemplates\Prompts\*.md" />
</ItemGroup>
```

```csharp
// In your service registration
services.AddAITemplatesFromAssembly(typeof(MyService).Assembly);
```

---

## Using Liquid Templates

Prompt bodies support full Liquid syntax. Use camelCase for all template variable names:

```markdown
---
Title: Task Planning
---

You are a planning assistant.

{% if userTools.size > 0 %}
## Available User Tools
{{ userTools }}
{% endif %}

{% if systemTools.size > 0 %}
## System Tools
{{ systemTools }}
{% endif %}
```

### Including Other Prompts

Use the `include_prompt` filter to compose prompts:

```liquid
{{ "use-markdown-syntax" | include_prompt }}

You are a helpful assistant.
```

This renders the `use-markdown-syntax` prompt template inline.

---

## Using the Service

### IAITemplateService

The main service interface for working with prompts:

```csharp
public interface IAITemplateService
{
    Task<IReadOnlyList<AITemplate>> ListAsync();
    Task<AITemplate> GetAsync(string id);
    // Throws KeyNotFoundException if the template ID is not found.
    Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null);
    Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = "\n\n");
}
```

:::note
`RenderAsync` throws a `KeyNotFoundException` if the specified template ID does not exist. This ensures that missing templates are caught early during development rather than silently returning null. Always make sure your template files are properly registered before calling `RenderAsync`.
:::

### Examples

```csharp
// Inject the service
public class MyService
{
    private readonly IAITemplateService _templateService;

    public MyService(IAITemplateService templateService)
    {
        _templateService = templateService;
    }

    public async Task DoWorkAsync()
    {
        // Render a simple prompt
        var prompt = await _templateService.RenderAsync("my-prompt");

        // Render with arguments
        var dynamicPrompt = await _templateService.RenderAsync("task-planning", new Dictionary<string, object>
        {
            ["userTools"] = "search, calculator",
            ["systemTools"] = "file-reader",
        });

        // Merge multiple prompts
        var combined = await _templateService.MergeAsync(
            ["use-markdown-syntax", "my-prompt"],
            separator: "\n\n");

        // List all available prompts
        var allPrompts = await _templateService.ListAsync();
    }
}
```

### AITemplateBuilder

For composing prompts from multiple sources (raw strings, `AITemplate` objects, and template IDs), use the `AITemplateBuilder`. It uses pooled buffers to minimize allocations:

```csharp
// Build from raw strings and AITemplate objects (synchronous)
var result = new AITemplateBuilder()
    .WithSeparator("\n\n")
    .Append("You are a helpful assistant.")
    .Append(someAITemplate)
    .Append("Always respond in English.")
    .Build();

// Build with template ID resolution (asynchronous, requires IAITemplateService)
var result = await new AITemplateBuilder()
    .WithSeparator("\n\n")
    .Append("Base instructions.")
    .AppendTemplate("rag-response-guidelines")
    .AppendTemplate("use-markdown-syntax")
    .BuildAsync(templateService);
```

---

## Extending Parsers

The library ships with a Markdown front matter parser (`DefaultMarkdownAITemplateParser`) which handles `.md` files. To add support for other formats (e.g., YAML, JSON), implement `IAITemplateParser`:

```csharp
public class YamlAITemplateParser : IAITemplateParser
{
    public IReadOnlyList<string> SupportedExtensions => [".yaml", ".yml"];

    public AITemplateParseResult Parse(string content) { /* ... */ }
}
```

Register your parser in DI:

```csharp
services.TryAddEnumerable(ServiceDescriptor.Singleton<IAITemplateParser, YamlAITemplateParser>());
```

Providers (`FileSystemAITemplateProvider`, `EmbeddedResourceAITemplateProvider`, `ModuleAITemplateProvider`) automatically select the correct parser based on file extension.

---

## Orchard Core Integration

When the `CrestApps.OrchardCore.AI.Prompting` feature is enabled:

- **Module Discovery** — Prompts in `AITemplates/Prompts/` directories of all Orchard Core modules are automatically discovered.
- **Feature Filtering** — Prompts placed in `AITemplates/Prompts/{featureId}/` subdirectories are only available when that feature is enabled.
- **Caching** — Templates are parsed once and cached in memory. The cache is invalidated when the tenant shell is released or the application restarts.
- **UI Integration** — A prompt selection dropdown is added to AI Profile and Chat Interaction editors:
  - Prompts are grouped by category in the dropdown using `<optgroup>` elements.
  - Selecting a template shows its description (which documents available parameters).
  - A **Template Parameters** field accepts JSON key-value pairs for passing arguments to Liquid templates.
  - Choosing **Custom Instructions** (the default) allows free-form system message input.

---

## Using in Non-OrchardCore Projects

The standalone `CrestApps.AI.Prompting` library can be used in any .NET project:

```csharp
// In your Program.cs or Startup.cs
services.AddAIPrompting();

// Optionally configure discovery paths
services.Configure<AITemplateOptions>(options =>
{
    options.DiscoveryPaths.Add(Path.Combine(AppContext.BaseDirectory));
});
```

This registers:
- `IAITemplateParser` — Parses front matter metadata (markdown by default; extensible). JSON blocks inside fenced code blocks are automatically compacted.
- `IAITemplateEngine` — Processes Liquid templates (rendering and validation)
- `IAITemplateService` — Main service for listing, getting, and rendering prompts
- `IAITemplateProvider` — File system and options-based providers

---

## Validation

The CI pipeline validates all prompt template files on every pull request:

- Checks for valid front matter structure (matching `---` delimiters)
- Verifies files are not empty
- **Validates fenced ` ```json ``` ` blocks** — If a fenced JSON block contains invalid JSON and doesn't appear to be a schema description (e.g., containing `true | false` or `<placeholder>` patterns), the CI fails. This catches accidental JSON typos that could cause the AI model to produce malformed output.

To validate locally:

```csharp
var engine = serviceProvider.GetRequiredService<IAITemplateEngine>();
bool isValid = engine.TryValidate(templateBody, out var errors);
```
