---
Title: Data Source Availability Instructions
Description: Instructs the AI how to use the configured data source and its search tool.
Parameters:
  - tools: array of AIToolDefinitionEntry objects for data-source tools available.
  - searchToolName: Name of the data-source search tool when tools are enabled.
IsListable: false
Category: Data Sources
---

[Configured Data Source]

A data source is configured for this conversation.

Use any retrieved data-source context already present in the system message when it is relevant to the user's request.

{% if searchToolName %}
If the user asks for facts, summaries, or references that could come from the configured data source, call `{{ searchToolName }}` before answering whenever you need more context.

Do not conclude that the configured data source lacks relevant information until after searching it.

### Available data source tools:
{% for tool in tools %}
- `{{ tool.Name }}`: {{ tool.Description | strip }}
{% endfor %}
{% endif %}
