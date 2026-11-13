using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Assessments",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AssessmentsConstants.Features.Assessments,
    Name = "Learning Assessments",
    Description = "Question banks of nine question types, the quiz course item with fixed and random questions, attempts that copy each question so they never depend on a question version, deadlines kept by the server, essay grading, regrades with a reason, overrides of time, attempts and window, and the exam player.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
    ]
)]

[assembly: Feature(
    Id = AssessmentsConstants.Features.Gradebook,
    Name = "Learning Gradebook",
    Description = "Grade scales that map a percentage to a letter or to pass and fail, and the gradebook of each course with every learner and every graded item.",
    Category = "Learning",
    Dependencies =
    [
        AssessmentsConstants.Features.Assessments,
    ]
)]
