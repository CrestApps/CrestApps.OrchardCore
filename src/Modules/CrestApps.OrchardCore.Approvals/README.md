# Approvals

The `CrestApps.OrchardCore.Approvals` feature records approvals for any module: a request asks for a decision about a
subject (a pay run, a time sheet, an expense), a policy says who may decide and how many must, and every decision is
kept with who made it, when and why.

## What it gives other modules

- `IApprovalService` (in `CrestApps.OrchardCore.Approvals.Abstractions`): `RequestAsync`, `GetEligibilityAsync`,
  `DecideAsync`, `CancelAsync` and `GetApproverUserIdsAsync`. Asking again while a request about the subject is pending
  returns that request.
- `IApprovalSubjectHandler`, registered with `services.AddApprovalSubjectHandler<T>()`: told when a request about its
  subject kind is approved, rejected or withdrawn, so the module moves its record on.
- `IApprovalObserver`, registered with `services.AddApprovalObserver<T>()`: told about every change after it is saved.
- The source access seam: a request carries its subject kind and access scope, so the module that owns the subject
  decides who may see and decide its requests.

## Policies

A policy names approvers with rules (anyone with a permission, anyone in a role, the subject's manager and the managers
above through Staff, or named staff members), how many different approvers must approve, and an optional due time.
Two rules are built in and on by default: the person who asked can't approve, and nobody decides a request about
themselves.

## Notices, stages and reminders

- The approver's inbox is Orchard's notification center: the `Approval` and `ApprovalDecided` notification types, each
  person choosing delivery per type. Notices about a request are removed when it is decided.
- Workflows: Approval Requested, Approval Approved and Approval Rejected events; Request Approval, Approve Request,
  Reject Request and Get Approval Request tasks. Stages are workflows that ask for the next approval when one is
  approved.
- The **Automation: remind approvers** recipe adds a workflow with a Timer Event that reminds the approvers every
  morning while a request waits.

## Data

- Recipe steps `ApprovalPolicies` and `ApprovalRequests` (history only), deployment steps for both.
- The **Approvals** data source with the **Approval requests** data set, for reports and data pipelines.
