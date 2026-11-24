using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Classes;
using CrestApps.OrchardCore.Scheduling;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Classes",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ClassesConstants.Feature,
    Name = "Learning Classes",
    Description = "The class course item: dated sessions in Scheduling with a capacity, a location or a meeting link and a registration window, registration of enrolled learners with a waitlist and offers, a roster to mark attendance, and attendance that completes the class.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        SchedulingConstants.Features.Scheduling,
    ]
)]
