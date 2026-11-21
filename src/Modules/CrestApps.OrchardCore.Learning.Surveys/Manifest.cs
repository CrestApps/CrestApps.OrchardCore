using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Surveys;
using CrestApps.OrchardCore.Surveys;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Surveys",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = LearningSurveysConstants.Feature,
    Name = "Learning Surveys",
    Description = "The feedback survey and choice course items. Each item owns a survey of the Surveys module, designed with every question type, logic and anonymity, and learners answer it through their enrollment. The item completes when the learner answers; the totals come from the Surveys reports.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        SurveysConstants.Features.Surveys,
    ]
)]
