using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.AI;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning AI",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = LearningAIConstants.Features.Assistant,
    Name = "Learning Assistant",
    Description = "A tutor AI profile template that answers learners from the lessons of the courses they are enrolled in, never from questions, quizzes or answer keys; a per-course switch, off by default; and the Learning AI tools: my progress, my next lesson, explain my quiz result, and overdue and expiring learners within the caller's scope.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = LearningAIConstants.Features.Authoring,
    Name = "Learning Authoring Assistant",
    Description = "Author prompts that draft a course outline, questions from a lesson, a rubric or a translation as unpublished drafts for an author to review, and scoring help that suggests a score and feedback to a grader, who confirms or changes it.",
    Category = "Learning",
    Dependencies =
    [
        LearningAIConstants.Features.Assistant,
    ]
)]
