# Surveys AI

AI for the Surveys module, in the feature `CrestApps.OrchardCore.Surveys.AI` (depends on Surveys and CrestApps AI).

- **AI tools**: `list_surveys`, `get_survey_results` (through each question type's analyzer, for people who may read the responses; anonymous surveys below their minimum group size show nothing) and `create_survey_draft` (writes a definition checked against the definition JSON schema and saves it as a draft; never publishes).
- **Summarize answers with AI**: a page per survey that summarizes the latest written answers to a question into themes and sentiment.
- **Translate with AI**: translates the draft's texts into one of the site's cultures; the author reviews them on the survey's translations page and publishes.
- **Scoring help**: on the Surveys grading page, a button suggests points and a comment for an answer that needs a grader, and the grade the grader saves is marked AI-assisted.

Prompts live in `Templates/Prompts/*.md`.
