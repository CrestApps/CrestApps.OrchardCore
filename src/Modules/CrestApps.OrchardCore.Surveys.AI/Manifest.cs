using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Surveys;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Surveys AI",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = SurveysConstants.Features.AI,
    Name = "Surveys AI",
    Description = "AI for surveys: AI tools that list surveys, read their results and draft a new survey for review, a summary of the answers to a written question, a translation of a survey into another culture, and scoring help that suggests a score and comment to a grader, who decides.",
    Category = "Surveys",
    Dependencies =
    [
        SurveysConstants.Features.Surveys,
        AIConstants.Feature.Area,
    ]
)]
