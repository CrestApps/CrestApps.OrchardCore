using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Surveys;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Surveys",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = SurveysConstants.Feature,
    Name = "Learning Surveys",
    Description = "The feedback survey and choice course items: rating, single choice, multiple choice and written questions, anonymous answers, a close date, totals learners may see, and a totals page for the course staff. The item completes when the learner answers.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
    ]
)]
