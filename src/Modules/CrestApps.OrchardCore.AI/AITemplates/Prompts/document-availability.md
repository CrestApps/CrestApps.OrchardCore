---
Title: Document Availability Instructions
Description: Instructs the AI about uploaded documents and available tools.
Parameters:
	- tools: array of AIToolDefinitionEntry objects for document processing tools available.
	- availableDocuments: array of ChatInteractionDocumentInfo objects with DocumentId, FileName, ContentType, and FileSize.
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
- {{ tool.Name }}: {{ tool.Description }}
{% endfor %}
{% else %}
The user has uploaded the following documents as supplementary context.
{% endif %}

{% if availableDocuments.size > 0 %}
{% for doc in availableDocuments %}
- {{ doc.DocumentId }}: "{{ doc.FileName }}" ({{ doc.ContentType | default: "unknown" }}, {{ doc.FileSize }} bytes)
{% endfor %}
{% endif %}
