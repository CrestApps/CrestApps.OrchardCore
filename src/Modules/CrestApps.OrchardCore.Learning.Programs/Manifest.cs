using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Users;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Programs",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ProgramsConstants.Features.Paths,
    Name = "Learning Paths",
    Description = "Learning paths: ordered sets of courses joined by then, and and or, each set completed by every course, any one or a number of them. Starting a path enrolls the person in its first stage, and each completed stage opens the next.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
    ]
)]

[assembly: Feature(
    Id = ProgramsConstants.Features.RequiredLearning,
    Name = "Required Learning",
    Description = "Requires a course, a learning path or a certification path of a user audience or of named people, with a due date, reminders before it, escalation to the manager after it, exemptions with evidence, a nightly run and Run now.",
    Category = "Learning",
    Dependencies =
    [
        ProgramsConstants.Features.Paths,
        UsersConstants.Feature.Audiences,
    ]
)]

[assembly: Feature(
    Id = ProgramsConstants.Features.TeamLearning,
    Name = "Team Learning",
    Description = "Shows managers the required learning of the people who report to them, directly or not, without their answers or attempts.",
    Category = "Learning",
    Dependencies =
    [
        ProgramsConstants.Features.RequiredLearning,
        StaffConstants.Features.Staff,
    ]
)]
