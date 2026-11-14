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
    Description = "Approvals for any module: policies that say who may decide (a permission, a role, the manager or named staff) and how many must, the record of each decision, the approver's inbox in the notification center, and workflow events and tasks for stages, reminders and escalations.",
    Category = "Workflows"
)]
