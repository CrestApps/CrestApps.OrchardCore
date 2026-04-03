---
Title: Post-Session Analysis Prompt
Description: User prompt containing tasks and conversation transcript for post-session analysis
IsListable: false
Category: Analysis
Parameters:
  - tasks: array of task objects with Name, Type, Instructions, AllowMultipleValues, and Options
  - prompts: array of prompt objects with Role and Content
---

Analyze the following completed chat conversation and produce results for the requested tasks.
Tasks to process:
{% for task in tasks %}
- {{ task.Name }} (type: {{ task.Type }}){% if task.Instructions %}: {{ task.Instructions }}{% endif %}{% if task.Type == "PredefinedOptions" and task.Options.size > 0 %}{% if task.AllowMultipleValues %} [allowMultiple=true]{% endif %} Options: [{% for option in task.Options %}{% if forloop.index0 > 0 %}, {% endif %}{{ option.Value }}{% if option.Description %} ({{ option.Description }}){% endif %}{% endfor %}]{% endif %}
{% endfor %}

[Conversation transcript]
{% for prompt in prompts %}
{{ prompt.Role }}: {{ prompt.Content | strip }}
{% endfor %}
