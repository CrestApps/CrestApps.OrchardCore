---
Title: Extracted Data Availability
Description: Shows already collected session fields so chat flows do not re-ask for them.
Parameters:
  - collectedFields: array of collected field objects with Name, Description, Values, AllowMultipleValues, and IsUpdatable
  - missingFields: array of field objects still missing from the session
IsListable: false
Category: Chat
---

[Collected Session Data]

The current chat session already contains the following structured data.

Treat these values as already known for this session. Do not ask for them again unless the user is correcting or updating them.

Collected fields:
{% for field in collectedFields %}
- `{{ field.Name }}`{% if field.Description %} — {{ field.Description }}{% endif %}: [{% for value in field.Values %}{% if forloop.index0 > 0 %}, {% endif %}{{ value }}{% endfor %}]
{% endfor %}

{% if missingFields and missingFields.size > 0 %}
Fields still missing:
{% for field in missingFields %}
- `{{ field.Name }}`{% if field.Description %} — {{ field.Description }}{% endif %}
{% endfor %}
{% endif %}

If you need to continue collecting information, only ask for fields that are still missing or that the user explicitly wants to change.
