---
Title: Data Extraction Prompt
Description: User prompt containing extraction targets, current state, and the latest chat turn
IsListable: false
Category: Extraction
Parameters:
  - fields: array of field objects with Name, Description, AllowMultipleValues, and IsUpdatable
  - currentState: array of extracted field objects with Name and Values
  - lastAssistantMessage: the last assistant message before the latest user message
  - lastUserMessage: the latest user message to extract from
---

Extract the following fields from the user's latest message.

Fields to extract:
{% for field in fields %}
- {{ field.Name }}{% if field.Description %}: {{ field.Description }}{% endif %} (multiple: {{ field.AllowMultipleValues | downcase }}, updatable: {{ field.IsUpdatable | downcase }})
{% endfor %}

{% if currentState and currentState.size > 0 %}
Current extracted state:
{% for field in currentState %}
- {{ field.Name }}: [{% for value in field.Values %}{% if forloop.index0 > 0 %}, {% endif %}{{ value }}{% endfor %}]
{% endfor %}
{% endif %}

{% if lastAssistantMessage %}
Last assistant message: {{ lastAssistantMessage }}
{% endif %}

Latest user message: {{ lastUserMessage }}
