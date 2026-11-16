# Approvals

The `CrestApps.OrchardCore.Approvals` feature records approvals for any module: a module registers an approval type
(a pay run, a ticket, a time sheet) and asks for approval of one of its records, a policy chosen by type and amount says
who decides, in what stages and how many must approve each, and every decision is kept with who made it, when and why.

## What it gives other modules

All in `CrestApps.OrchardCore.Approvals.Abstractions`:

- `services.AddApprovalType<THandler>(name, ...)`: an approval type with a display name and a handler derived from
  `ApprovalTypeHandlerBase`, whose hooks fill and check a request (`RequestingAsync`), check a decision
  (`DecidingAsync`), and follow it (`StageApprovedAsync`, `ApprovedAsync`, `RejectedAsync`, `CanceledAsync`) in the
  decision's unit of work. A type keeps its own data on the request with `Put`.
- `services.AddApproverRuleProvider<T>()`: a way to name approvers (`IApproverRuleProvider`), beside the built-in
  Permission, Role and Users, and Manager and Staff with Staff on. Its value editor in the policy editor is the
  `ApproverRuleValue_Edit__{Kind}` shape alternate.
- `IApprovalService`: `RequestAsync` (with no policy named, `IApprovalPolicyResolver` picks the first enabled policy
  whose types and conditions match), `GetEligibilityAsync`, `DecideAsync`, `CancelAsync` and `GetApproverUserIdsAsync`.
- `IApprovalObserver`, registered with `services.AddApprovalObserver<T>()`: told about every change after it is saved.
- Display drivers of `ApprovalRequest` (`DetailAdmin`, `SummaryAdmin`) and `ApprovalPolicy`, to add a type's fields to
  the request page, the rows and the policy editor.
- The source access seam: a request carries its type and access scope, so the module that owns the type decides who
  sees its requests through the View approval requests permission.

## Policies and stages

A policy lists the approval types it serves, an order, optional amount, currency and scope conditions, and up to ten
stages decided one after the other, each with approver rules and the approvals it needs. A policy without stages is one
stage. Two rules are built in, on by default and checked at every stage: the person who asked can't approve, and
nobody decides a request about themselves. One rejection at any stage rejects the request.

## Who sees what

- **Waiting for me**: the requests whose current stage names the reader, with no Approvals permission needed.
- **My requests**: the reader's own requests, which they may withdraw.
- **All requests**: with View approval requests, what the source access allows.

## Notices and reminders

- The approver's inbox is also Orchard's notification center: the `Approval` and `ApprovalDecided` notification
  types. Each stage's approvers are told; notices are removed when the stage or the request closes.
- Workflows: Approval Requested, Approval Approved and Approval Rejected events (filtered by approval type); Request
  Approval (with type, amount and currency), Approve Request, Reject Request and Get Approval Request tasks.
- The **Automation: remind approvers** recipe adds a workflow with a Timer Event that reminds the approvers every
  morning while a request waits.

## Data

- Recipe steps `ApprovalPolicies` and `ApprovalRequests` (history only), deployment steps for both.
- The **Approvals** data source with the **Approval requests** data set (with the type and stage), for reports and
  data pipelines.
