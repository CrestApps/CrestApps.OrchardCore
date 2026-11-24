using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Packages;
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

[assembly: Feature(
    Id = AssessmentsConstants.Features.QtiImport,
    Name = "Learning QTI Import",
    Description = "Imports QTI 3.0 and QTI 2.1 assessment items (a content package or a single item file) as questions through Bulk Import, into a question bank you choose, with their images in the media library.",
    Category = "Learning",
    Dependencies =
    [
        AssessmentsConstants.Features.Assessments,
        ContentTransferConstants.Feature.ModuleId,
        OrchardCoreFeatureIds.Media,
    ]
)]

[assembly: Feature(
    Id = AssessmentsConstants.Features.MoodleImport,
    Name = "Learning Moodle Import",
    Description = "Imports Moodle course backups (.mbz, zip or tar.gz) through Bulk Import on the Course type: the course in its category with its dates, enrolment and completion, its sections, and an item for each SCORM package, quiz (with its questions), file, link and page. Each activity, question or setting it does not import is reported as a failed row, and importing a course again updates it.",
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
