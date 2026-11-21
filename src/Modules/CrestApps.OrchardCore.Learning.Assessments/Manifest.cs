using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Surveys;
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
    Description = "The quiz course item. Each quiz owns a scored survey of the Surveys module: its questions and question banks, pools, time limit, attempts, pass mark, review options, grading and accommodations. A learner's attempts are survey responses through their enrollment, and the quiz grade combines them with the quiz's grading method.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        SurveysConstants.Features.Surveys,
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

[assembly: Feature(
    Id = AssessmentsConstants.Features.MoodleImport,
    Name = "Learning Moodle Import",
    Description = "Imports Moodle course backups (.mbz, zip or tar.gz) through Bulk Import on the Course type: the course in its category with its dates, enrolment and completion, its sections, and an item for each SCORM package, quiz (a survey with its questions and question banks), feedback, choice, file, link and page. Each activity, question or setting it does not import is reported as a failed row, and importing a course again updates it.",
    Category = "Learning",
    Dependencies =
    [
        AssessmentsConstants.Features.Assessments,
        PackagesConstants.Features.Scorm,
        LearningConstants.Features.CourseCategories,
        ContentTransferConstants.Feature.ModuleId,
        OrchardCoreFeatureIds.Media,
    ]
)]
