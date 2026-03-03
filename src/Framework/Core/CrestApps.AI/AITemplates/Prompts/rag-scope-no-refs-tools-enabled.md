---
Title: RAG Scope - No References, Tools Enabled
Description: Scope constraint when no knowledge source content was found but search tools are available
IsListable: false
Category: RAG
---

[Scope Constraint]
No relevant content was found during the initial search of the configured knowledge sources.
CRITICAL: You MUST only answer based on knowledge source content. DO NOT use your general knowledge or training data.
Before concluding that no answer is available, you MUST call the available search tools (e.g., search_data_source, search_documents) to look for relevant information.
If the search tools also return no relevant results, you MUST inform the user that the answer is not available in the current knowledge sources.
