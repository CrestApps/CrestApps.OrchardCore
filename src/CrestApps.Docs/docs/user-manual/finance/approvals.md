---
sidebar_label: Approvals
title: Approvals
description: "Decide what others ask you to approve, such as a pay run or a ticket, from Waiting for me; set up who approves each type of request, in what stages and for which amounts; and remind approvers while a request waits."
technical_manual:
  - modules/approvals
---

Some work needs someone else's yes before it goes ahead: a pay run before it pays, a time sheet before it is paid, a large expense before it is reimbursed. The request comes to the people allowed to decide it, sometimes in stages (the manager, then finance); they approve or reject it, and the work moves on.

| | |
| --- | --- |
| **Menu** | Approvals > Requests, Approvals > Policies |
| **Permission** | None to decide what you are asked; View approval requests and Manage approval requests to see others' requests; Manage approval policies |
| **Feature** | Approvals |

<AskYourAdmin />

## Decide a request

1. Open the notice in your notifications, or open **Approvals > Requests**. **Waiting for me** lists the requests waiting for your decision; filter them by **Type**, such as pay runs or tickets.
2. Choose **Open** to see the type, what is asked, by whom, the amount, the stages and the decisions so far. **Open what this is about** shows the record itself.
3. Choose **Approve**, or write why in **Note** and choose **Reject**. A rejection always needs a note: the person who asked sees it.

A request can have stages, such as **Manager** then **Finance**: each stage waits until enough of its approvers approve, then the next stage's approvers are asked. One rejection at any stage rejects the request. You decide one stage of a request at most.

You can't decide a request you asked for, or one about yourself (your own pay or time), unless the policy allows it.

You don't need any Approvals permission to decide what a policy asks you to decide, such as your team's time sheets. You do need to be allowed to act on the record itself: for a pay run, to approve pay runs for its employer.

## Follow and withdraw your requests

**My requests** lists the requests you made, with their status. Open one and choose **Withdraw** to take it back while it waits. People who manage approval requests can withdraw any; people who view approval requests see **All requests**.

## Set who may approve

1. Open **Approvals > Policies** and choose **Add approval policy**. Choose what the policy approves, such as **Pay run**: the form then shows only the fields that type needs. To change a policy, choose **Edit**; what it approves can't change.
2. Give it a **Title** and a **Technical name**. A feature that names its policy asks for it by name (Payroll asks for `PayRunApproval`).
3. If it is only for some requests, fill in the conditions its type offers, such as an **Amount** range and currency, or a **Scope** such as one employer for pay runs. Policies are tried by **Order**, lowest first; the first enabled one that matches a request is used. Clear **Choose this policy for requests that name none** to use it only when a feature names it.
4. Under **Stage 1**, add the rules of who decides first: anyone with a permission, anyone in a role, named users, the manager of the person the request is about, or named staff members (features can add more, such as a ticket's team). Set **Approvals needed**.
5. To add a second or third stage, fill in its rules the same way. A stage without rules isn't used.
6. Set **Due after (hours)** if requests should be decided within a time, and leave the built-in rules off unless you are a very small team where the same person must ask and approve, or decide more than one stage (**Let one person decide more than one stage**).
7. Choose **Save**. A warning tells you when fewer people can decide a stage today than it needs.

Changing a policy doesn't change requests already made.

A policy saved before policies had one type, and that served several types or every type, shows **General** as what it approves: it still serves any type and shows every condition.

## Remind approvers

Apply the recipe **Automation: remind approvers** from **Configuration > Recipes**. It adds a workflow, turned off, that reminds the approvers every weekday at 9 while a request waits. Open **Workflows > Remind approvers of pending requests** to change the time, then turn it on. A workflow's approval events can be limited to one approval type.

## Choose how you are told

Your notification preferences list **Approvals to decide** and **Approval decisions**. Choose for each whether it also comes by email or text message.
