using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Certifications;
using CrestApps.OrchardCore.Skills;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Certifications",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = CertificationsConstants.Features.Certifications,
    Name = "Learning Certifications",
    Description = "Certification paths: a Skills certification earned by completing courses and passing assessments, issued with a number, a verification page and a printed certificate, renewed in a window before it expires as a new cycle that keeps the earlier one, lapsed at expiry and stopped by a revocation.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        SkillsConstants.Features.IssuedCertificates,
    ]
)]

[assembly: Feature(
    Id = CertificationsConstants.Features.Competencies,
    Name = "Learning Competencies",
    Description = "Competencies: a course or a learning path grants Skills skills at a level when a learner completes it, and learners see the competencies they hold. A competency framework is a skill category.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        SkillsConstants.Features.Skills,
    ]
)]
