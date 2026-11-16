using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Coursework;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Coursework",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = CourseworkConstants.Feature,
    Name = "Learning Coursework",
    Description = "The coursework course item: work learners hand in as text or files, with a due date, late marking and resubmission; rubrics with criteria and levels; grading against a rubric or out of points with feedback, regrades with a reason, the grade counted in the course grade, and the submissions in the grading list.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        AssessmentsConstants.Features.Gradebook,
    ]
)]
