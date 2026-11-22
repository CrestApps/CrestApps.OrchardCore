# Surveys

Surveys, polls, forms and quizzes for Orchard Core, as a building block other modules use (Learning runs its course surveys and quizzes on it).

## Features

| Feature | Id | What it adds |
| --- | --- | --- |
| Surveys | `CrestApps.OrchardCore.Surveys` | Surveys, versions, the designer, question types, logic, randomization, pools, question banks, links, access rules and limits, responses, scoring, grading and results |
| Survey Invitations | `CrestApps.OrchardCore.Surveys.Invitations` | Personal invitations and reminders |
| Survey Embedding | `CrestApps.OrchardCore.Surveys.Embedding` | The Survey part and widget, and the `[survey]` shortcode |
| QTI Question Import | `CrestApps.OrchardCore.Surveys.Qti` | QTI import into question banks |
| Surveys API | `CrestApps.OrchardCore.Surveys.Api` | A JSON API |

## Authoring

- **Surveys** (admin menu **Surveys → Surveys**) lists the surveys on `<admin-list>` with search and kind, status and tag filters. Surveys owned by another feature (such as a course item) are hidden unless **Show** is set to include them.
- **New survey** starts from a template: blank, customer satisfaction, Net Promoter Score, employee engagement, event feedback, course feedback, quiz or poll. Modules add templates with `services.AddSurveyTemplateProvider<T>()`.
- **The designer** edits the draft only. The outline adds, moves (buttons or drag and drop), copies and deletes pages and questions; the side panel edits a question (`IDisplayManager<SurveyQuestion>`: common settings, choices, rows, validation, scoring, and the settings driver of the question type) or a page (visibility, branches, pools and bank draws). Survey-wide settings (`IDisplayManager<SurveyDefinition>`) cover presentation, navigation, scoring and review, timing, endings, variables, score categories and bands. Rules are edited with the rule builder, which writes the shared `ConditionRule` model; the server checks every rule again.
- **Publish** checks the draft (`ISurveyDefinitionValidator`), copies the question bank questions each pool draws, compares it with the published version (`ISurveyDefinitionDiffer`, with changes that affect existing answers flagged), and creates an immutable version. The first publish opens the survey and creates its default public link.
- **Versions** lists the published versions with their responses; a version can be viewed, compared with the draft, or restored into the draft.
- **Collect** lists the collectors: public links and kiosks, each with its window, response limit, password, hidden variables, language and QR code.
- **Access and settings** holds the live settings that apply without a new version: audience, anonymity, IP address, open and close times, weekly schedule, respondent limits, most responses, CAPTCHA, abandoning unfinished responses, retention and the minimum group size of anonymous reports.
- **Settings → Surveys** holds the defaults of new surveys, the largest upload and the hosts endings may redirect to.

## Extending

Every concept is reached through `Surveys.Abstractions`: `AddSurveyControl<THandler>` for a question type, `AddSurveyAccessRule<T>`, `AddSurveyResponseObserver<T>`, `AddSurveyObserver<T>`, `AddSurveyAccessProvider<T>`, `AddSurveyGradingScope<T>`, `AddSurveyDefinitionValidationContributor<T>`, `AddSurveyTemplateProvider<T>`, `AddSurveyImportFormat<T>` and `AddSurveyContextKind`.

See the technical manual at `src/CrestApps.Docs/docs/modules/surveys/` and the user manual at `src/CrestApps.Docs/docs/user-manual/surveys/`.
