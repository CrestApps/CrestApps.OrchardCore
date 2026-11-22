using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Resources;
using CrestApps.OrchardCore.Surveys;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Surveys",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = SurveysConstants.Features.Surveys,
    Name = "Surveys",
    Description = "Surveys, polls, forms and quizzes: a designer with pages, logic, randomization and question pools, immutable versions, public links, access rules and limits, anonymous responses, scoring and grading, and results reports.",
    Category = "Surveys",
    Dependencies =
    [
        OrchardCoreFeatureIds.Users,
        ResourcesConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = SurveysConstants.Features.Invitations,
    Name = "Survey Invitations",
    Description = "Personal survey invitations with their own links and reminders, sent to users, user audiences and e-mail addresses. E-mail invitations skip the addresses that opted out through Outreach and carry an unsubscribe link.",
    Category = "Surveys",
    Dependencies =
    [
        SurveysConstants.Features.Surveys,
        OrchardCoreFeatureIds.Notifications,
        OutreachConstants.Feature,
    ]
)]

[assembly: Feature(
    Id = SurveysConstants.Features.Embedding,
    Name = "Survey Embedding",
    Description = "The Survey part and widget, and the survey shortcode, to show a survey in a page.",
    Category = "Surveys",
    Dependencies =
    [
        SurveysConstants.Features.Surveys,
        OrchardCoreFeatureIds.Contents,
    ]
)]

[assembly: Feature(
    Id = SurveysConstants.Features.Qti,
    Name = "QTI Question Import",
    Description = "Imports IMS QTI 2.1 and 3.0 items and packages into question banks.",
    Category = "Surveys",
    Dependencies =
    [
        SurveysConstants.Features.Surveys,
        OrchardCoreFeatureIds.Media,
    ]
)]

[assembly: Feature(
    Id = SurveysConstants.Features.Api,
    Name = "Surveys API",
    Description = "A JSON API to list surveys, start and answer responses, and read responses.",
    Category = "Surveys",
    Dependencies =
    [
        SurveysConstants.Features.Surveys,
    ]
)]
