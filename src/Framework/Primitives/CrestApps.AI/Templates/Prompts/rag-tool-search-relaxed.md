---
Title: RAG Tool Search - Relaxed
Description: Instructions requiring the model to use search tools first but allowing general knowledge as fallback
Parameters:
  - searchToolNames: Array of available search tool names.
IsListable: false
Category: RAG
---

[Knowledge Source Instructions]
IMPORTANT: You have access to internal knowledge sources via search tools{% if searchToolNames and searchToolNames.size > 0 %} ({% for toolName in searchToolNames %}`{{ toolName }}`{% unless forloop.last %}, {% endunless %}{% endfor %}){% endif %}.
You MUST call the relevant search tools to check for information BEFORE generating any response. Do NOT skip this step.
After reviewing the search results:
1. If relevant results are found, use them as the primary basis for your answer and cite using reference markers (e.g., [doc:1], [doc:2]) inline immediately after the relevant statement.
2. If no relevant results are found, you may then use your general knowledge to answer the question.
Always search first, then respond.
