---
Title: Conversion Goal Evaluation Prompt
Description: User prompt containing goals and conversation transcript for conversion goal evaluation
IsListable: false
Category: Analysis
Parameters:
  - goals: array of goal objects with Name, Description, MinScore, and MaxScore
  - prompts: array of prompt objects with Role and Content
---

Evaluate the following conversation against each goal and assign a score.

Goals:
{% for goal in goals %}
- {{ goal.Name }}: {{ goal.Description }} (score range: {{ goal.MinScore }}-{{ goal.MaxScore }})
{% endfor %}

Conversation transcript:
{% for prompt in prompts %}
{{ prompt.Role }}: {{ prompt.Content }}
{% endfor %}
