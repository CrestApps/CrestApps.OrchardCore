---
sidebar_label: Approvals
title: Approvals
description: "Approvals for any module: approval requests about a subject, policies that name approvers by permission, role, manager or staff and how many must approve, the two built-in rules, the approver's inbox in the notification center, subject handlers, source access, workflow events and tasks, a reminder recipe, the Approval requests data set and recipe and deployment steps."
user_manual:
  - user-manual/finance/approvals
---

The **Approvals** module records approvals for any module. A module asks for an approval about one of its records (the subject), a policy says who may decide and how many must, and every decision is kept with who made it, when and why. Payroll asks for pay run approval; time sheets, expenses and purchase orders can ask the same way.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Approvals` | Approvals | none (uses Staff, Notifications and Workflows when they are on) |

Projects: `CrestApps.OrchardCore.Approvals.Abstractions` (models and contracts), `CrestApps.OrchardCore.Approvals.Core` (store, rules, the approver directory, the notification observer), `CrestApps.OrchardCore.Approvals` (the module).

## Policies

An `ApprovalPolicy` (named catalog entry, `INamedCatalogManager<ApprovalPolicy>`) has a technical `Name` modules and workflows ask for, a `Title`, a `Description`, `Approvers` (a list of `ApproverRule`), `RequiredApprovals` (1 to 10 different approvers), `AllowRequesterToApprove`, `AllowSubjectToDecide` and `DueAfterHours`.

| `ApproverKind` | `Value` | Who it names |
| --- | --- | --- |
| `Permission` | A permission's name | Anyone the authorization service grants it (roles, implied permissions, administrators) |
| `Role` | A role's name | Anyone in the role |
| `Manager` | none | The managers up the chain of the staff member the request is about (`SubjectStaffMemberId`, through Staff's `IStaffHierarchy`) |
| `Staff` | Staff member ids, comma separated | The users of those staff members |

A person any rule names may decide. Two rules are built in and on by default: the requester can't approve (`AllowRequesterToApprove`), and a person listed in the request's `SubjectUserIds` can't decide it (`AllowSubjectToDecide`). A person decides a request once. One rejection rejects; a request is approved once `RequiredApprovals` different approvers approved. A request keeps the number of approvals it needed when the policy changes later.

## Requests

An `ApprovalRequest` (YesSql document in the `Approvals` collection, `ApprovalRequestIndex`) holds `SubjectKind`, `SubjectId`, `AccessScope`, `SubjectUrl`, `SubjectUserIds`, `SubjectStaffMemberId`, `Title`, an optional `Amount` and `Currency`, `PolicyId`, `PolicyTitle`, `RequiredApprovals`, the requester, `Status` (`Pending`, `Approved`, `Rejected`, `Canceled`), `Decisions` (user, kind, note, time), `CancelReason`, `CreatedUtc`, `DueUtc`, `ClosedUtc` and `IsHistorical` for imported ones. The index holds no amount.

```csharp
var result = await approvalService.RequestAsync(new ApprovalSubmission
{
    Policy = "PayRunApproval",
    SubjectKind = "PayRun",
    SubjectId = payRun.ItemId,
    AccessScope = payRun.EmployerId,
    SubjectUrl = $"~/Admin/payroll/runs/{payRun.ItemId}",
    SubjectUserIds = [preparedByUserId],
    Title = $"Approve the pay run of {period}",
    Amount = payRun.NetTotal,
    Currency = payRun.Currency,
}, User);
```

`IApprovalService`:

| Method | Does |
| --- | --- |
| `RequestAsync(submission, requester)` | Asks under a policy (id or name). Asking again while a request about the subject is pending under the policy returns that request. |
| `GetEligibilityAsync(request, user)` | Whether the person may decide, and why not. |
| `DecideAsync(request, user, kind, note)` | Approves or rejects; rejecting needs a note. |
| `CancelAsync(request, reason)` | Withdraws a pending request. |
| `GetApproverUserIdsAsync(request)` | The users who may still decide, for notices. |

## Extension points

- `IApprovalSubjectHandler` (`services.AddApprovalSubjectHandler<T>()`): `SubjectKind` and `ClosedAsync(request)`, called when a request about that kind is approved, rejected or withdrawn, so the module moves its record on.
- `IApprovalObserver` (`services.AddApprovalObserver<T>()`): `OnChangedAsync(change)` after every change (`Requested`, `PartlyApproved`, `Approved`, `Rejected`, `Canceled`).
- Source access: the module that owns the subject kind registers an `ISourceAccess` for it (see [Disbursements](./disbursements.md#who-sees-which-disbursements)). The request list, the decide page, the export, the data set and the workflow tasks show and act only on the requests the person's scope allows; viewing uses the View level, deciding and withdrawing the Manage level.

## Notifications

With `OrchardCore.Notifications` on, the notification center is the approver's inbox. Two notification types are registered with `AddNotificationType`: `Approval` (**Approvals to decide**) and `ApprovalDecided` (**Approval decisions**); each person chooses delivery per type in their profile. Approvers get a notice that opens the decide page (`ReferenceId` is the request id); the notices of a request are deleted once it is decided or withdrawn, and the requester is told the decision.

## Workflows

| Activity | Kind | Notes |
| --- | --- | --- |
| Approval Requested, Approval Approved, Approval Rejected | Events | Correlation id is the request id. Input: `RequestId`, `SubjectKind`, `SubjectId`, `Title`, `PolicyId`, `PolicyTitle`, `Status`, `Change`, `RequesterName`, `ApproverUserNames`, `Decider`, `Note`, `DueUtc`. |
| Request Approval | Task | Policy, subject kind and id, access scope, title, subject page (Liquid). Outcomes `Done`, `Failed`; writes `ApprovalRequestId`. |
| Approve Request, Reject Request | Tasks | Request id and note (Liquid). Decides as the person the workflow runs for, under the same rules; a workflow that runs for nobody can't decide. Outcomes `Done`, `NotAllowed`, `NotFound`. |
| Get Approval Request | Task | Outcomes `Pending`, `Approved`, `Rejected`, `Canceled`, `NotFound`; an empty id reads the correlation id. |

Stages are workflows: on Approval Approved for the first policy, Request Approval under the next. The **Automation: remind approvers** recipe (`ApprovalsReminder`) adds a disabled workflow: Approval Requested, then a Timer Event (`0 9 * * 1-5`, local time), Get Approval Request and, while pending, Notify User Task to `{{ Workflow.Input.ApproverUserNames }}` with notification type `Approval`, looping back to the timer.

## Data

- **Approvals** data source, **Approval requests** data set (`ApprovalRequests`): one row per request with its policy, status, amount, approvals needed and given, requester, deciders, rejection note, dates and hours to decide. Gated by `ViewApprovalRequests` and the source access seam; `ModifiedUtc` is the incremental column.
- Recipe steps: `ApprovalPolicies` (`Policies`, matched by id then name) and `ApprovalRequests` (`Requests`, history only: never decided again, no notice; a pending one is imported withdrawn).
- Deployment steps: **Approval policies**, and **Approval requests** (requires `ViewApprovalRequests`; a plan run without a signed-in person writes only the requests no subject kind restricts).

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageApprovalPolicies` | Add, edit and delete approval policies. |
| `ViewApprovalRequests` | See the approval requests the source access allows, and decide those a policy names you for. |
| `ManageApprovalRequests` | Withdraw anyone's pending request. Implies View. |
