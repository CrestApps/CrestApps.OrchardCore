using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Xapi;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Packages",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = PackagesConstants.Features.Scorm,
    Name = "Learning SCORM Packages",
    Description = "The learning package course item for SCORM 1.2 and SCORM 2004 (3rd and 4th Edition) packages: authors import a package, learners run it in a sandboxed frame that cannot reach their session, the run-time data is checked and kept on the server, and the result counts in the learner's progress, marked as reported by the browser.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
    ]
)]

[assembly: Feature(
    Id = PackagesConstants.Features.Cmi5,
    Name = "Learning cmi5 Packages",
    Description = "cmi5 (Quartz) courses in the learning package course item: authors import a package with cmi5.xml, learners launch its AUs (files of the package in the sandboxed frame, https AUs in a window of their own) against the Learning Record Store with a registration per enrollment, the cmi5 statement rules are enforced, and the course's moveOn counts in the learner's progress, marked as reported by the browser.",
    Category = "Learning",
    Dependencies =
    [
        PackagesConstants.Features.Scorm,
        XapiConstants.Features.LearningRecordStore,
    ]
)]
