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
{% if isInScope %}
Answer only from the uploaded documents and retrieved document context.
If the documents do not contain the answer, clearly say that the answer is not available in the uploaded documents.
Do not use your general knowledge to fill gaps.
{% else %}
If the documents contain relevant information, base your answer on that content.
If the documents do not contain relevant information, use your general knowledge to answer instead.
Do not refuse to answer simply because the documents lack the requested information.
{% endif %}

### Available document tools:
{% for tool in tools %}
- {{ tool.Name }}: {{ tool.Description }}
{% endfor %}
{% else %}
The user has uploaded the following documents as supplementary context.
{% if isInScope %}
Answer only from the uploaded documents.
If the uploaded documents do not contain the answer, say so instead of using general knowledge.
{% endif %}
{% endif %}

{% if userSuppliedDocuments.size > 0 %}
### Available documents:
{% for doc in userSuppliedDocuments %}
- {{ doc.DocumentId }}: "{{ doc.FileName }}" ({{ doc.ContentType | default: "unknown" }}, {{ doc.FileSize }} bytes)
{% endfor %}
{% endif %}
{% endif %}

{% if knowledgeBaseDocuments.size > 0 %}
Background knowledge is available for this profile.
{% if tools.size > 0 %}
Search the profile knowledge documents first using the available document tools before answering.
### Available document tools:
{% for tool in tools %}
- {{ tool.Name }}: {{ tool.Description }}
{% endfor %}
{% endif %}
Use the available document tools and background context to answer accurately.
{% if isInScope %}
Stay grounded in the retrieved profile knowledge and document context only.
If the available knowledge does not contain the answer, say that you do not have enough retrieved information.
{% endif %}
Do not mention knowledge-base documents, files, uploads, or attachments unless the user explicitly uploaded files in this session.
{% endif %}
