---
sidebar_label: AI
title: Surveys AI
description: "The Surveys AI module: AI tools that list surveys, read results and draft surveys, answer summaries, translation with AI, and scoring help for graders."
user_manual:
  - user-manual/surveys/ai-help
---

**Surveys AI** (`CrestApps.OrchardCore.Surveys.AI`, module *Surveys AI*) depends on Surveys and CrestApps AI. Its prompts are templates in `Templates/Prompts/` (`surveys-score-answer`, `surveys-draft-survey`, `surveys-summarize-answers`, `surveys-translate`).

## AI tools

| Tool | Does |
| --- | --- |
| `list_surveys` | Lists the surveys the user may manage |
| `get_survey_results` | Summarizes a survey's results through each question type's analyzer; checks access to the survey and the minimum group size |
| `create_survey_draft` | Builds a survey definition from a prompt or a document, checks it against the definition schema, and saves it as a draft; it never publishes |

See [AI tools](../../ai/tools.md).

## Pages

- **Summarize answers** (`admin/surveys/{id}/ai/summarize`): themes and sentiment of the latest 200 answers to a written question of the published version. It never names a respondent, and refuses below the minimum group size of an anonymous survey.
- **Translate with AI** (`admin/surveys/{id}/ai/translate`): translates the draft's texts into a culture in batches, optionally replacing existing translations; the author reviews them on the Translations page.
- **Suggest a score with AI**: a button on the grading page for answers a grader scores. It suggests points and a comment and records the AI help on the answer (`ISurveyGradingService.RecordScoringHelpAsync`); the grader decides and saves.

The two survey pages appear in the survey list's actions menu.
