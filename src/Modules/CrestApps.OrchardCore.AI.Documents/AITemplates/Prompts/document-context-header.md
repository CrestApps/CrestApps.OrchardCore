---
Title: Document Context Header
Description: Header for uploaded document RAG context injection.
Parameters:
	- searchToolName: the name of the search tool for additional lookups (optional).
IsListable: false
Category: RAG
---

[Uploaded Document Context]
The following content was retrieved from the user's uploaded documents via semantic search. Use this information to answer the user's question accurately.
If the documents do not contain relevant information, use your general knowledge to answer instead.
When citing information, include the corresponding reference marker (e.g., [doc:1]) inline in your response immediately after the relevant statement.
{% if searchToolName %}
If you need additional context, use the '{{ searchToolName }}' tool to search for more content in the uploaded documents.
{% endif %}
