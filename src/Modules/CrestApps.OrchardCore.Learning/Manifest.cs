using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Resources;
using CrestApps.OrchardCore.Users;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = LearningConstants.Features.Learning,
    Name = "Learning",
    Description = "Courses built from Orchard content: an outline of lessons (pages, videos, files, links and acknowledgements), enrollment with grants, approval and access dates, progress and completion decided on the server with a completion record that keeps what the learner did, release rules, and lesson access that the display route, the content API, GraphQL and Secure Media all follow.",
    Category = "Learning",
    Dependencies =
    [
        OrchardCoreFeatureIds.Contents,
        OrchardCoreFeatureIds.Lists,
        OrchardCoreFeatureIds.Title,
        OrchardCoreFeatureIds.Html,
        OrchardCoreFeatureIds.Autoroute,
        OrchardCoreFeatureIds.Media,
        OrchardCoreFeatureIds.ContentFields,
        OrchardCoreFeatureIds.Roles,
        UsersConstants.Feature.Area,
        ResourcesConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = LearningConstants.Features.LearnerPages,
    Name = "Learner Pages",
    Description = "The pages a learner uses: My learning, the course player for lessons, videos, files, links and acknowledgements, and the transcript of completed courses.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
    ]
)]

[assembly: Feature(
    Id = LearningConstants.Features.SelfEnrollment,
    Name = "Self Enrollment",
    Description = "Lets a signed-in learner enroll in a course on its page, at once, with the course's enrollment key, or pending approval, as the course allows.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.LearnerPages,
    ]
)]

[assembly: Feature(
    Id = LearningConstants.Features.CourseCategories,
    Name = "Course Categories",
    Description = "A tree of course categories from an Orchard Core taxonomy: a category field on the Course type, category browsing in the course catalog, and a category filter in the admin list of courses. A site that already has a \"Course categories\" taxonomy or a category field on courses keeps it.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        OrchardCoreFeatureIds.Taxonomies,
        OrchardCoreFeatureIds.TaxonomiesContentsAdminList,
    ]
)]
