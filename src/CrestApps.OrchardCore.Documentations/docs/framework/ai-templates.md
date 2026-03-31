---
sidebar_label: AI Templates
sidebar_position: 7
title: AI Templates
description: Liquid-based prompt template engine for managing, rendering, and composing AI system prompts.
---

# AI Templates

> A Liquid-based template engine for managing, rendering, and composing AI system prompts from multiple sources.

## Quick Start

```csharp
builder.Services.AddAIPrompting();
```

:::info
You rarely need to call this directly — `AddCrestAppsAI()` chains it automatically.
:::

## Problem & Solution

Hard-coding system prompts in C# makes them difficult to maintain, localize, and customize. The template system:

- Stores prompts as **markdown files** with front-matter metadata
- Renders them with **Liquid** syntax for dynamic content
- Discovers templates from **multiple sources** (embedded resources, file system, code)
- Supports **merging** multiple templates into a single prompt

## Services Registered by `AddAIPrompting()`

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `IAITemplateParser` | `DefaultMarkdownAITemplateParser` | Singleton | Parses markdown front-matter templates |
| `IAITemplateEngine` | `FluidAITemplateEngine` | Singleton | Renders Liquid templates |
| `IAITemplateService` | `DefaultAITemplateService` | Scoped | Unified template discovery and rendering |
| `OptionsAITemplateProvider` | — | Singleton | Templates registered via code |
| `FileSystemAITemplateProvider` | — | Singleton | Templates discovered from disk |

## Key Interfaces

### `IAITemplateService`

The main service for working with templates.

```csharp
public interface IAITemplateService
{
    Task<IReadOnlyList<AITemplate>> ListAsync();
    Task<AITemplate> GetAsync(string id);
    Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null);
    Task<string> MergeAsync(
        IEnumerable<string> ids,
        IDictionary<string, object> arguments = null,
        string separator = "\n\n");
}
```

### `IAITemplateEngine`

Renders Liquid templates. You can replace this with a custom engine.

```csharp
public interface IAITemplateEngine
{
    Task<string> RenderAsync(string template, IDictionary<string, object> arguments);
    bool TryValidate(string template, out IReadOnlyList<string> errors);
}
```

### `IAITemplateProvider`

Implement to supply templates from a custom source (database, API, etc.).

```csharp
public interface IAITemplateProvider
{
    Task<IReadOnlyList<AITemplate>> GetTemplatesAsync();
}
```

## Template File Format

Templates are markdown files with YAML front matter, stored in `AITemplates/Prompts/`:

```markdown
---
Title: Helpful Assistant
Description: A general-purpose helpful assistant prompt
Category: General
IsListable: true
---
You are a helpful assistant. Today's date is {{ "now" | date: "%Y-%m-%d" }}.

{% if user_name %}
You are assisting {{ user_name }}.
{% endif %}
```

### Front Matter Fields

| Field | Type | Description |
|-------|------|-------------|
| `Title` | string | Display name |
| `Description` | string | Human-readable description |
| `Category` | string | Grouping category |
| `IsListable` | bool | Whether the template appears in listing APIs |

## Registering Templates

### From Embedded Resources

Store `.md` files as embedded resources under `AITemplates/Prompts/` in your assembly:

```csharp
builder.Services.AddAITemplatesFromAssembly(typeof(MyClass).Assembly, source: "MyApp");
```

### From Code

```csharp
builder.Services.AddAIPrompting(options =>
{
    options.AddTemplate("my-template", "You are {{ role }}.", metadata =>
    {
        metadata.Title = "Role Template";
    });
});
```

### From File System

```csharp
builder.Services.AddAIPrompting(options =>
{
    options.AddDiscoveryPath("/app/templates");
});
```

## Configuration

### `AITemplateOptions`

```csharp
services.Configure<AITemplateOptions>(options =>
{
    options.AddDiscoveryPath("/path/to/templates");
    options.AddTemplate(new AITemplate { /* ... */ });
});
```

## Template Examples

### Example 1: Customer Support Assistant

```markdown title="AITemplates/Prompts/customer-support.md"
---
Title: Customer Support Assistant
Description: Prompt for a customer-facing support chatbot
Category: Support
IsListable: true
---
You are a customer support assistant for {{ company_name | default: "our company" }}.

## Your Responsibilities
- Answer product questions accurately and concisely.
- Help customers troubleshoot common issues.
- Escalate to a human agent when you cannot resolve an issue.

## Guidelines
- Always be polite and professional.
- Never share internal pricing or unreleased product details.
- Today's date is {{ "now" | date: "%B %d, %Y" }}.

{% if support_hours %}
Our support hours are {{ support_hours }}. If the customer is contacting us outside
these hours, let them know when support will be available.
{% endif %}

{% if knowledge_base_context %}
## Relevant Knowledge Base Articles
{{ knowledge_base_context }}
{% endif %}
```

### Example 2: Content Writer with Tone Control

```markdown title="AITemplates/Prompts/content-writer.md"
---
Title: Content Writer
Description: Generates content with configurable tone and style
Category: Content
IsListable: true
---
You are a professional content writer.

**Tone**: {{ tone | default: "professional" }}
**Target audience**: {{ audience | default: "general" }}
**Max length**: {{ max_words | default: "500" }} words

{% case tone %}
{% when "casual" %}
Write in a friendly, conversational style. Use contractions and simple language.
{% when "formal" %}
Write in a formal, authoritative style. Avoid contractions and colloquialisms.
{% when "technical" %}
Write with precision. Use domain-specific terminology and cite sources where possible.
{% endcase %}
```

### Example 3: RAG-Augmented Assistant

```markdown title="AITemplates/Prompts/rag-assistant.md"
---
Title: RAG Assistant
Description: Assistant that uses retrieved documents for grounded answers
Category: RAG
IsListable: false
---
You are a knowledgeable assistant. Answer questions using ONLY the provided context.

## Rules
- If the context does not contain enough information, say so honestly.
- Always cite which document or section your answer comes from.
- Do not make up information that is not in the provided context.

{% if retrieved_documents %}
## Retrieved Context
{% for doc in retrieved_documents %}
### Document: {{ doc.title }}
{{ doc.content }}
{% endfor %}
{% endif %}
```

## Liquid Reference

The template engine uses the [Fluid](https://github.com/sebastienros/fluid) Liquid implementation. Here are the most commonly used filters and tags in AI templates:

### Filters

| Filter | Example | Output |
|--------|---------|--------|
| `date` | `{{ "now" \| date: "%Y-%m-%d" }}` | `2025-01-15` |
| `default` | `{{ name \| default: "User" }}` | `User` (if name is nil) |
| `upcase` | `{{ "hello" \| upcase }}` | `HELLO` |
| `downcase` | `{{ "HELLO" \| downcase }}` | `hello` |
| `truncate` | `{{ text \| truncate: 100 }}` | First 100 characters |
| `strip_html` | `{{ html_content \| strip_html }}` | Plain text |
| `escape` | `{{ user_input \| escape }}` | HTML-escaped string |
| `size` | `{{ items \| size }}` | Number of items |
| `join` | `{{ tags \| join: ", " }}` | Comma-separated string |

### Tags

```liquid
{% comment %} Conditional content {% endcomment %}
{% if variable %}...{% elsif other %}...{% else %}...{% endif %}

{% comment %} Loops {% endcomment %}
{% for item in collection %}{{ item.name }}{% endfor %}

{% comment %} Switch/case {% endcomment %}
{% case variable %}{% when "value1" %}...{% when "value2" %}...{% endcase %}

{% comment %} Variable assignment {% endcomment %}
{% assign greeting = "Hello, " | append: user_name %}
```

## Template Composition

For complex prompts, compose templates using the `MergeAsync` method instead of creating one monolithic template:

```csharp
// Render multiple templates and merge into a single prompt
var systemPrompt = await templateService.MergeAsync(
    ["base-personality", "safety-rules", "rag-instructions"],
    arguments: new Dictionary<string, object>
    {
        ["user_name"] = currentUser.DisplayName,
        ["company"] = tenant.Name,
    },
    separator: "\n\n---\n\n"
);
```

Best practices for template composition:

- **Base templates** — Define shared personality, tone, and general rules.
- **Feature templates** — Add instructions for specific capabilities (RAG, tool use, safety).
- **Context templates** — Inject dynamic, session-specific content (user info, retrieved docs).

:::tip
Keep each template focused on a single concern. Compose them at runtime rather than duplicating instructions across templates. This makes it easy to update one aspect (e.g., safety rules) without touching every template.
:::

## Testing Templates

### Validate Liquid Syntax

Use `IAITemplateEngine.TryValidate()` to check for syntax errors before saving a template:

```csharp
var engine = serviceProvider.GetRequiredService<IAITemplateEngine>();

var template = "Hello {{ user_name }}, today is {{ 'now' | date: '%A' }}.";

if (engine.TryValidate(template, out var errors))
{
    Console.WriteLine("Template is valid.");
}
else
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Error: {error}");
    }
}
```

### Render with Test Arguments

Use `IAITemplateService.RenderAsync()` to preview the final output:

```csharp
var result = await templateService.RenderAsync("customer-support", new Dictionary<string, object>
{
    ["company_name"] = "Contoso",
    ["support_hours"] = "9 AM – 5 PM EST",
    ["knowledge_base_context"] = "Article: How to reset your password...",
});

// Inspect the rendered output
Console.WriteLine(result);
```

### Unit Testing Templates

```csharp
public sealed class TemplateRenderingTests
{
    [Fact]
    public async Task CustomerSupportTemplate_ShouldIncludeCompanyName()
    {
        // Arrange
        var engine = new FluidAITemplateEngine();
        var template = "You are a support agent for {{ company_name }}.";
        var arguments = new Dictionary<string, object>
        {
            ["company_name"] = "TestCorp",
        };

        // Act
        var result = await engine.RenderAsync(template, arguments);

        // Assert
        Assert.Contains("TestCorp", result);
    }

    [Fact]
    public void InvalidTemplate_ShouldReturnErrors()
    {
        var engine = new FluidAITemplateEngine();
        var invalid = "Hello {{ user_name | nonexistent_filter }}";

        var isValid = engine.TryValidate(invalid, out var errors);

        Assert.False(isValid);
        Assert.NotEmpty(errors);
    }
}
```

## Custom Template Provider

Implement `IAITemplateProvider` to load templates from a custom source (e.g., a database or remote API):

```csharp
public sealed class DatabaseAITemplateProvider(
    ISession session,
    IAITemplateParser parser) : IAITemplateProvider
{
    public async Task<IReadOnlyList<AITemplate>> GetTemplatesAsync()
    {
        var records = await session
            .Query<PromptTemplateRecord, PromptTemplateIndex>()
            .ListAsync();

        var templates = new List<AITemplate>();

        foreach (var record in records)
        {
            // Parse the markdown content (with YAML front-matter) into an AITemplate
            if (parser.TryParse(record.Content, out var template))
            {
                template.Id = record.TemplateId;
                template.Source = "Database";
                templates.Add(template);
            }
        }

        return templates;
    }
}
```

Register the provider:

```csharp
builder.Services.AddSingleton<IAITemplateProvider, DatabaseAITemplateProvider>();
```

:::info
All registered `IAITemplateProvider` instances are queried by `IAITemplateService`. Templates from multiple providers are merged into a single collection. If two providers return templates with the same `Id`, the last-registered provider wins.
:::

## Orchard Core Integration

The [AI Prompt Templates module](../ai/prompt-templates.md) adds admin UI for creating and editing templates, deployment steps, and recipe support.
