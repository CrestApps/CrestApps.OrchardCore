---
Title: User Memory Context Header
Description: Introduces preemptively retrieved user memory context for the current authenticated user.
Parameters:
  - searchToolName: Optional tool name that can be used to search user memory again if more context is needed.
  - results: Array of retrieved memory results to render as prompt context.
IsListable: false
Category: Memory
---

[Retrieved User Memory]

The following persistent user-memory entries were retrieved as likely relevant context for the current request.

Use them when they help answer accurately.

{% if searchToolName %}
If you still need more user-specific memory, call `{{ searchToolName }}` before guessing.
{% endif %}

Treat these entries as private user-scoped context. Do not mention internal memory keys unless they naturally help answer the user.

{% for result in results %}
---
{% if result.Name %}Memory: {{ result.Name }}
{% endif %}{% if result.Description %}Description: {{ result.Description }}
{% endif %}Content: {{ result.Content }}
{% if result.UpdatedUtc %}UpdatedUtc: {{ result.UpdatedUtc }}
{% endif %}
{% endfor %}
