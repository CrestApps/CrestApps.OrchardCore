---
Title: Document Availability Instructions
Description: Instructs the AI about uploaded documents and available tools. Parameters - tools (array of objects with name and description, optional): document processing tools available.
IsListable: false
Category: Documents
---

[Available Documents or attachments]
{% if tools.size > 0 %}
The user has uploaded the following documents as supplementary context.
Search the uploaded documents first using the document tools before answering.
If the documents contain relevant information, base your answer on that content.
If the documents do not contain relevant information, use your general knowledge to answer instead.
Do not refuse to answer simply because the documents lack the requested information.

Available document tools:
{% for tool in tools %}
- {{ tool.name }}: {{ tool.description }}
{% endfor %}
{% else %}
The user has uploaded the following documents as supplementary context.
{% endif %}
