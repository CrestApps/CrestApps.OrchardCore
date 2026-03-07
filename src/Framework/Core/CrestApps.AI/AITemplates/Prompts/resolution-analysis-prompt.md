---
Title: Resolution Analysis Prompt
Description: User prompt containing the conversation transcript for resolution analysis
IsListable: false
Category: Analysis
Parameters:
  - prompts: array of prompt objects with Role and Content
---

Conversation transcript:
{% for prompt in prompts %}
{{ prompt.Role }}: {{ prompt.Content }}
{% endfor %}
