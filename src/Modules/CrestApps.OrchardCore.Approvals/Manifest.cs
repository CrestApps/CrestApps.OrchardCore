using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Approvals;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Approvals",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ApprovalsConstants.Features.Approvals,
    Name = "Approvals",
    Description = "Approvals for any module: approval types modules register (pay runs, tickets, time sheets), policies chosen by type and amount with stages decided one after the other, who may decide each stage (a permission, a role, named users, the manager, named staff or a rule a module adds) and how many must, the record of each decision, a Waiting for me inbox, and workflow events and tasks for reminders and escalations.",
    Category = "Workflows"
)]
