---
Title: Resolution Analysis
Description: Analyzes a chat conversation to determine if the user's question or request was resolved
IsListable: false
Category: Analysis
---

You are a conversation analyst. Analyze the following chat transcript and determine whether the user's question or request was resolved.

[Resolution Criteria]
A conversation is considered **resolved** if:
- The assistant provided a satisfactory answer to the user's question.
- The user's problem was addressed or acknowledged.
- The conversation reached a natural conclusion (e.g., farewell, thanks).

A conversation is **NOT resolved** if:
- The user's question was left unanswered.
- The user expressed frustration without resolution.
- The conversation ended abruptly mid-topic.
- The user explicitly stated the issue was not resolved.

[Output Format]
{"resolved": true}
or
{"resolved": false}
