---
sidebar_label: Approvals
title: Approvals
description: "Decide what others ask you to approve, such as a pay run, set up who may approve each kind of request, and remind approvers while a request waits."
technical_manual:
  - modules/approvals
---

Some work needs someone else's yes before it goes ahead: a pay run before it pays, a time sheet before it is paid. The request comes to the people allowed to decide it; they approve or reject it, and the work moves on.

| | |
| --- | --- |
| **Menu** | Approvals > Requests, Approvals > Policies |
| **Permission** | View approval requests; Manage approval requests; Manage approval policies |
| **Feature** | Approvals |

<AskYourAdmin />

## Decide a request

1. Open the notice in your notifications, or open **Approvals > Requests**. Requests marked **Yours to decide** are waiting for you.
2. Choose **Open** to see what is asked, by whom, the amount and the decisions so far. **Open what this is about** shows the record itself.
3. Choose **Approve**, or write why in **Note** and choose **Reject**. A rejection always needs a note: the person who asked sees it.

If a request needs more than one approval, it stays waiting until enough different people approve. One rejection rejects it.

You can't decide a request you asked for, or one about yourself (your own pay or time), unless the policy allows it.

## Withdraw a request

Open the request and choose **Withdraw**. The person who asked can withdraw their own; people who manage approval requests can withdraw any.

## Set who may approve

1. Open **Approvals > Policies** and choose **Add approval policy**, or **Edit** an existing one.
2. Give it a **Title** and the **Technical name** your features ask for (for example, Payroll asks for `PayRunApproval`).
3. Under **Who may decide**, add one or more rules: anyone with a permission, anyone in a role, the manager of the person the request is about, or named staff members.
4. Set **Approvals needed** and, if requests should be decided within a time, **Due after (hours)**.
5. Leave the two built-in rules off unless you are a very small team where the same person must ask and approve.
6. Choose **Save**.

Changing a policy doesn't change requests already made.

## Remind approvers

Apply the recipe **Automation: remind approvers** from **Configuration > Recipes**. It adds a workflow, turned off, that reminds the approvers every weekday at 9 while a request waits. Open **Workflows > Remind approvers of pending requests** to change the time, then turn it on.

## Choose how you are told

Your notification preferences list **Approvals to decide** and **Approval decisions**. Choose for each whether it also comes by email or text message.
