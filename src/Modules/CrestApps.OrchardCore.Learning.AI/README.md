# CrestApps.OrchardCore.Learning.AI

AI for the CrestApps Learning module, built on the AI building blocks (profiles and templates, prompt templates, AI Chat, data sources, metering).

- **Learning tutor**: an AI profile template and a tutor switch on AI profiles. A tutor retrieves only page, video and file lessons of the courses the learner is actively enrolled in, whose learning assistant switch is on (off by default on every course).
- **Enrollment retrieval filter**: through the AI Data Sources result filter extension point (`IAIDataSourceResultFilter`), every data source search drops questions, question banks and quizzes for every profile and person, and trims course items to what the person may view.
- **Learning AI tools**: my progress, my next lesson, explain my quiz result (only as far as the quiz's review policy allows), and overdue and expiring learners within the caller's scope.
- **Authoring assistant**: author prompts that draft a course outline, questions from a lesson, a rubric or a translation as unpublished drafts.
- **Scoring help**: a suggested score and feedback on the essay and coursework grading pages; the grader confirms or changes it, and the grade records that it was AI-assisted.

See the [Learning AI documentation](https://orchardcore.crestapps.com/docs/modules/learning-ai).
