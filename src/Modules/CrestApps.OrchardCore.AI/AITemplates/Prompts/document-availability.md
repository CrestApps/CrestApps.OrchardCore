---
Title: Document Availability Instructions
Description: Instructs the AI about uploaded documents and available tools.
Parameters:
  - tools: array of AIToolDefinitionEntry objects for document processing tools available.
  - knowledgeBaseDocuments: array of profile-level ChatDocumentInfo objects that are hidden background knowledge.
  - userSuppliedDocuments: array of session/user-level ChatDocumentInfo objects that are user-visible uploads/attachments.
IsListable: false
Category: Documents
---

[Available Documents, attachments or files]

{% if userSuppliedDocuments.size > 0 %}
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

{% if userSuppliedDocuments.size > 0 %}
Available documents:
{% for doc in userSuppliedDocuments %}
- {{ doc.DocumentId }}: "{{ doc.FileName }}" ({{ doc.ContentType | default: "unknown" }}, {{ doc.FileSize }} bytes)
{% endfor %}
{% endif %}
{% endif %}

{% if knowledgeBaseDocuments.size > 0 %}
Background knowledge is available for this profile.
Use the available document tools and background context to answer accurately.
Do not mention knowledge-base documents, files, uploads, or attachments unless the user explicitly uploaded files in this session.
{% endif %}
