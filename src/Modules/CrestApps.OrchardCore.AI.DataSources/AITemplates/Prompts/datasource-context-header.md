---
Title: Data Source Context Header
Description: Header for data source RAG context injection. Parameters - searchToolName (string, optional): the name of the search tool for additional lookups.
IsListable: false
Category: RAG
---

[Data Source Context]
The following context was retrieved from the configured data source. Use this information to answer the user's question accurately and directly without mentioning or referencing the retrieval process.
When citing information, include the corresponding reference marker (e.g., [doc:1]) inline in your response immediately after the relevant statement.
{% if searchToolName %}

If you need additional context or more relevant information, use the '{{ searchToolName }}' tool to retrieve more documents from the data source.
{% endif %}
