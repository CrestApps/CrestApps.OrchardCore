using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.OrTools;
using CrestApps.OrchardCore.Workforce;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "OR-Tools",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = OrToolsConstants.Features.ShiftScheduling,
    Name = "OR-Tools Shift Scheduling",
    Description = "Auto-schedules shifts with Google OR-Tools' CP-SAT solver, starting from the built-in solver's schedule and keeping the better one.",
    Category = "Workforce",
    Dependencies =
    [
        WorkforceConstants.Features.ShiftPlanning,
    ]
)]

[assembly: Feature(
    Id = OrToolsConstants.Features.Dispatch,
    Name = "OR-Tools Dispatch",
    Description = "Optimizes a dispatch day with Google OR-Tools' routing solver, starting from the built-in optimizer's plan and keeping the better one.",
    Category = "Field Service",
    Dependencies =
    [
        DispatchConstants.Features.Dispatch,
    ]
)]
