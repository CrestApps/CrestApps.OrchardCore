---
sidebar_label: Approvals
title: Add an approval type
description: "Approvals is a building block any module extends: register an approval type with its handler, add approver rule providers, add fields to the request and policy pages with display drivers, and ask for approval from code or a workflow. Worked examples: ticket and time sheet approval."
user_manual:
  - user-manual/finance/approvals
---

The **Approvals** module records approvals for any module. A module registers an **approval type** (a pay run, a ticket, a time sheet) and asks for approval of one of its records; a **policy** chosen by type and amount says who decides, in what **stages**, and how many must approve each; every decision is kept with who made it, when and why. Payroll's pay run approval is built this way, and a ticket, time sheet, expense or purchase order approval is added the same way, without changing Approvals.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Approvals` | Approvals | none (uses Staff, Notifications and Workflows when they are on) |

Projects: `CrestApps.OrchardCore.Approvals.Abstractions` (models and contracts: reference it from your module's `.Core` or `.Abstractions`), `CrestApps.OrchardCore.Approvals.Core` (store, rules, providers, resolver, inbox), `CrestApps.OrchardCore.Approvals` (the module). Approvals records decisions; reminders and escalations stay in Workflows.

## How a request moves

1. A module calls `IApprovalService.RequestAsync` with an `ApprovalSubmission`: the type (`SubjectKind`), the record's id, an optional amount, currency and access scope, and optionally a policy.
2. The policy is the one named, or the first enabled policy (by `Order`) whose `Types` include the type and whose amount, currency and scope conditions match (`IApprovalPolicyResolver`). No match is an error that says so.
3. The request copies the policy's stages, then the type's handler runs `RequestingAsync`: it fills the title and the subject's page, adds approvers, stores its own data on the request, or refuses.
4. The current stage's approvers are told (the **Approvals to decide** notification). A person a rule of the stage names may decide; the type's handler runs `DecidingAsync` first and may refuse.
5. When a stage has its approvals, the next one starts (`StageApprovedAsync`, and its approvers are told). One rejection at any stage rejects the request (`RejectedAsync`); the last stage's approvals approve it (`ApprovedAsync`). A withdrawn or replaced request calls `CanceledAsync`. The closing hooks run in the decision's unit of work.

Two rules are built in, on by default and checked at every stage: the requester can't approve (`AllowRequesterToApprove`), and a person in the request's `SubjectUserIds` can't decide it (`AllowSubjectToDecide`). A person decides one stage of a request at most.

## Register a type

In your module's startup, or in its `.Core` service registration:

```csharp
services.AddApprovalType<TicketApprovalTypeHandler>("Ticket", type =>
{
    type.DisplayName = LocalizationSource.Create<TicketApprovalTypeHandler>("Ticket");
    type.Description = LocalizationSource.Create<TicketApprovalTypeHandler>("A ticket that needs a manager's yes before the work starts.");
});
```

The name is stored on requests and policies and never changes. The display name shows on the requests list (with a **Type** filter), on the request page, in the policy editor's **Approval types**, on workflow events, and in the **Type** column of the **Approval requests** data set. A type whose feature is off keeps its requests and policies; its requests get a handler that does nothing.

## Write the handler

Derive from `ApprovalTypeHandlerBase` and override the hooks the type needs:

| Hook | When | Typical use |
| --- | --- | --- |
| `RequestingAsync(ApprovalRequestingContext)` | Before the request is stored | Fill `Request.Title`, `SubjectUrl`, `SubjectUserIds`, `SubjectStaffMemberId`; `AddApprover(rule)` to every stage; `Request.Put(...)` the type's data; `Refuse(reason)`. |
| `DecidingAsync(ApprovalDecidingContext)` | Before a decision is recorded, once the person is an approver | `Refuse(reason)` when the record changed since it was asked (compare `Request.SubjectVersion`). |
| `StageApprovedAsync(request)` | A stage that isn't the last is approved | Move the record to the next step. |
| `ApprovedAsync`, `RejectedAsync`, `CanceledAsync` | The request closes | Move the record on, in the same unit of work. |
| `GetSubjectUrlAsync`, `GetSubjectDisplayAsync` | The request page | Link to and describe the record (defaults: `SubjectUrl`, `Title`). |

```csharp
public sealed class TicketApprovalTypeHandler : ApprovalTypeHandlerBase
{
    private readonly ITicketManager _tickets;

    public TicketApprovalTypeHandler(ITicketManager tickets)
    {
        _tickets = tickets;
    }

    public override async Task RequestingAsync(ApprovalRequestingContext context, CancellationToken cancellationToken = default)
    {
        if (await _tickets.FindByIdAsync(context.Request.SubjectId, cancellationToken) is not { } ticket || ticket.IsClosed)
        {
            context.Refuse("The ticket is closed.");

            return;
        }

        context.Request.Title ??= $"Ticket {ticket.Number}: {ticket.Subject}";
        context.Request.SubjectUrl ??= $"~/Admin/tickets/{ticket.ItemId}";
        context.Request.SubjectUserIds.Add(ticket.RequesterUserId);
        context.Request.Put(new TicketApprovalData { TeamId = ticket.AssigneeTeamId });
    }

    public override async Task ApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => await _tickets.MarkApprovedAsync(request.SubjectId, cancellationToken);

    public override async Task RejectedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => await _tickets.MarkRejectedAsync(request.SubjectId, request.Decisions[^1].Note, cancellationToken);
}
```

A request is a catalog item: a type keeps its own data on it with `Put` and reads it with `TryGet`, in handlers, providers and display drivers.

## Name approvers: approver rule providers

A stage's `Approvers` are `ApproverRule`s: a provider's name (`Kind`) and a value. Approvals ships:

| Provider (`Kind`) | Value | Who it names | Registered |
| --- | --- | --- | --- |
| `Permission` | A permission's name | Anyone the authorization service grants it | Always |
| `Role` | A role's name | Anyone in the role | Always |
| `Users` | User ids, comma separated (the user picker) | Those users | Always |
| `Manager` | none | The managers up the chain of `SubjectStaffMemberId` | With Staff on |
| `Staff` | Staff member ids, comma separated | Their users | With Staff on |

Add your own with `IApproverRuleProvider` and `services.AddApproverRuleProvider<T>()`. Deciding asks `IsNamedAsync`; notices, **Waiting for me** and the approver count ask `GetUserIdsAsync`. Set `DependsOnRequest` when who it names depends on each request.

```csharp
public sealed class AssigneeTeamApproverRuleProvider : IApproverRuleProvider
{
    private readonly ITeamDirectory _teams;

    public AssigneeTeamApproverRuleProvider(ITeamDirectory teams)
    {
        _teams = teams;
    }

    public string Name => "AssigneeTeam";

    public LocalizationSource DisplayName { get; } = LocalizationSource.Create<AssigneeTeamApproverRuleProvider>("The ticket's assignee team");

    public bool NeedsValue => false;

    public bool DependsOnRequest => true;

    public async Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => (await GetUserIdsAsync(rule, request, cancellationToken)).Contains(user.FindFirstValue(ClaimTypes.NameIdentifier));

    public async Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
        => request.TryGet<TicketApprovalData>(out var data) ? await _teams.GetMemberUserIdsAsync(data.TeamId, cancellationToken) : [];
}
```

The policy editor lists every provider in each rule row and shows the value editor of the one chosen: the `ApproverRuleValue_Edit` shape (a text box) with the alternate `ApproverRuleValue_Edit__{Kind}`. Add `Views/ApproverRuleValue.Edit-AssigneeTeam.cshtml` for your own; its model's `Editor` (`ApproverRuleValueEditorViewModel`) gives the `FieldName` to post under and the current `Value`. A rule whose provider's feature is off is kept and names nobody.

Rules stored before providers were open held a number (0 Permission, 1 Role, 2 Manager, 3 Staff); `ApproverRuleKindJsonConverter` reads it as the name, so old policies and requests load and decide unchanged.

## Policies

An `ApprovalPolicy` (`INamedCatalogManager<ApprovalPolicy>`) has a `Name` modules and workflows ask for, a `Title`, `Types` (empty serves every type), `Enabled` (chosen for requests that name none), `Order`, the conditions `MinimumAmount`, `MaximumAmount`, `Currency` and `AccessScope`, `Stages` (each a `Title`, `Approvers` and `RequiredApprovals`, ten at most), the two built-in rules and `DueAfterHours`. A policy without `Stages` is one stage made of its `Approvers` and `RequiredApprovals`, as policies saved before stages; `GetStages()` returns either. A request copies the stages when it is made, so a later change of the policy never changes a request already asked.

Time sheet example: a policy `TimesheetApproval` for the `Timesheet` type with two stages, **Manager** (`Manager`, 1 approval) then **Payroll** (`Role` Payroll Clerk, 1 approval); and a policy for expenses over 1,000 USD with `Order` 10 and three stages, before the general expense policy with `Order` 50.

## Ask for approval

From code:

```csharp
var result = await approvalService.RequestAsync(new ApprovalSubmission
{
    SubjectKind = "Timesheet",
    SubjectId = timesheet.ItemId,
    SubjectStaffMemberId = timesheet.StaffMemberId,
    SubjectUserIds = [timesheet.UserId],
    SubjectVersion = timesheet.Version,
    Title = $"Time sheet of {timesheet.PersonName}, week {timesheet.Week}",
    Amount = timesheet.Hours,
}, User);

if (!result.Succeeded)
{
    // result.Errors says why: no policy matches, the handler refused, ...
}
```

Name a policy with `Policy` (id or technical name) to use it whatever its conditions, as Payroll does with `PayRunApproval`. Asking again while a request is pending returns it; asking with another `SubjectVersion` withdraws it and asks again.

From a workflow: **Request Approval** takes the approval type, an optional policy, the subject's id, scope, title, page, amount and currency (Liquid), and writes `ApprovalRequestId`. The **Approval Requested**, **Approval Approved** and **Approval Rejected** events take an optional **Approval type**, so a workflow starts only for its type.

## Add fields with display drivers

The request page renders the request with display type `DetailAdmin`, and the list rows with `SummaryAdmin` and `SummaryAdminGrid`; the policy editor is an editor of `ApprovalPolicy`. Add a `DisplayDriver<ApprovalRequest>` whose `CanHandleModel` accepts only your type to show the ticket's details on the request page:

```csharp
public sealed class TicketApprovalDisplayDriver : DisplayDriver<ApprovalRequest>
{
    public override bool CanHandleModel(ApprovalRequest request)
        => request.SubjectKind == "Ticket";

    public override IDisplayResult Display(ApprovalRequest request, BuildDisplayContext context)
        => context.DisplayType == "DetailAdmin"
            ? View("TicketApproval_DetailAdmin", request).Location("Content:5")
            : null;
}
```

A `DisplayDriver<ApprovalPolicy>` adds fields to the policy editor the same way, stored on the policy with `Put`.

## Who sees and decides requests

| Who | Sees | Decides |
| --- | --- | --- |
| A person a rule of the current stage names | **Waiting for me**, and the request page (still, after deciding it) | Yes, with no Approvals permission |
| The requester | **My requests**, and the request page | No (unless the policy allows); may withdraw it |
| `ViewApprovalRequests` | **All requests** of the subject types their source access allows | Only where a rule names them |
| `ManageApprovalRequests` | as View | May withdraw any request their source access allows at the Manage level |

The module that owns a type registers an `ISourceAccess` for it (see [Banking: who sees which disbursements](./banking.md#who-sees-which-disbursements)); the list, export, data set and workflow tasks use it for people who see requests through the permission. Anyone else gets "not found" for a request, not even that it exists. **Waiting for me** looks at the oldest 1,000 pending requests (`ApprovalInbox.MaxPendingScanned`).

## Reference

`IApprovalService`:

| Method | Does |
| --- | --- |
| `RequestAsync(submission, requester)` | Asks under the named or chosen policy; one request at a time per subject (a lock). |
| `GetEligibilityAsync(request, user)` | Whether the person may decide the current stage, and why not. |
| `DecideAsync(request, user, kind, note)` | Approves or rejects; rejecting needs a note. One decision at a time per request, on a fresh read. |
| `CancelAsync(request, reason)` | Withdraws a pending request. |
| `GetApproverUserIdsAsync(request)` | The users who may still decide the current stage, for notices. |

Other extension points: `IApprovalObserver` (`services.AddApprovalObserver<T>()`) after every change (`Requested`, `PartlyApproved`, `StageApproved`, `Approved`, `Rejected`, `Canceled`); `IApprovalTypeCatalog` to list types and get their handlers.

**Notifications.** With `OrchardCore.Notifications` on, the notification center is the approver's inbox: `Approval` (**Approvals to decide**) for each stage's approvers, removed when the stage or the request closes, and `ApprovalDecided` (**Approval decisions**) for the requester.

**Workflows.** Events: Approval Requested, Approval Approved, Approval Rejected (correlation id is the request id; input `RequestId`, `SubjectKind`, `SubjectId`, `Title`, `PolicyId`, `PolicyTitle`, `Status`, `Change`, `RequesterName`, `ApproverUserNames`, `Decider`, `Note`, `DueUtc`, `Stage`, `Amount`, `Currency`). Tasks: Request Approval, Approve Request and Reject Request (decide as the person the workflow runs for; outcomes `Done`, `NotAllowed`, `NotFound`), Get Approval Request. The **Automation: remind approvers** recipe adds a disabled reminder workflow (a Timer Event and Notify User Task).

**Data.** The **Approval requests** data set (`ApprovalRequests`) has a row per request with its type, stage, policy, status, amount, approvals, requester, deciders and dates. Recipe and deployment steps `ApprovalPolicies` (with `Types`, `Enabled`, `Order`, the conditions and `Stages`) and `ApprovalRequests` (history only).

| Permission | Allows |
| --- | --- |
| `ManageApprovalPolicies` | Add, edit and delete approval policies. |
| `ViewApprovalRequests` | See all the requests the source access allows. Deciding needs a rule that names you, not this permission. |
| `ManageApprovalRequests` | Withdraw anyone's pending request. Implies View. |
