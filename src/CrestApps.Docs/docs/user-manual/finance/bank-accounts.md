---
sidebar_label: Bank accounts
title: Bank accounts
description: "Keep the business's bank accounts and the accounts of the people it pays in one place, with account numbers hidden, checked before payments go to them, and shown in full only to people allowed to see them."
technical_manual:
  - modules/banking
---

Bank accounts hold the accounts money is sent from and sent to: the business's own accounts, such as the one payroll is paid from, and the accounts of the people and organizations it pays, such as an employee's pay account. Checks, payments and payroll use these accounts instead of keeping account numbers of their own.

| | |
| --- | --- |
| **Menu** | Banking > Bank Accounts, and Banking > My Bank Accounts for your own |
| **Permission** | Manage bank accounts; Manage one's own bank accounts for your own; Reveal bank account numbers to see a full number |
| **Feature** | Banking |

<AskYourAdmin />

## Add a bank account

1. Open **Banking > Bank Accounts** and choose **Add bank account**.
2. Fill in the account and its bank, and choose **Save**.

| Field | What it does |
| --- | --- |
| **Name** | The name the account is listed by, such as Payroll funding. |
| **Owner** | Who the account belongs to: a staff member, a user, a customer account or another kind of owner. Leave it empty for one of the business's own accounts. |
| **Account holder** | The name on the account, as the bank knows it. |
| **Account type** | Checking or Savings. |
| **Bank name**, **Country**, **Currency** | The bank, its two-letter country code (such as US) and the account's currency (such as USD). |
| **Routing number** | The bank's number. A United States routing number has nine digits, and a number with a wrong digit is refused. |
| **Account number** | Stored protected. After you save, only its last digits show, such as ••••6789. To change it, type the new number; leave it empty to keep it. |

A new account is **Unverified**: no payment goes to it until it is checked.

## Check an account

Open the account and set **Status** under **Verification**:

| Status | Means |
| --- | --- |
| **Unverified** | Entered, or its number changed, and not checked yet. |
| **Verification sent** | A check was started and no answer has come back yet. |
| **Verified** | Checked: payments can go to it. |
| **Failed** | The check failed, or the bank sent a payment back. |
| **Closed** | No longer used. A closed account never opens again; add a new one instead. |

Write in **Reason** how you checked it, such as "Called the bank". When a feature that checks accounts for you is on, the page offers a **Check with** button for it.

Changing the routing number or the account number always takes the account back to **Unverified**.

## See a full account number

People with **Reveal bank account numbers** see **Show the full number** at the bottom of the account page. The number opens on a page of its own, and every time someone opens it, it is recorded in the Audit Trail with who and when. Ask your administrator to keep the Audit Trail on: the site also writes each opening to its log, but the Audit Trail is where you can look it up.

## Remove an account

**Delete** in an account's menu removes an account nothing uses. An account that checks or payments were made from or to can't be deleted, since they still name it: the page says why. Close it instead: set **Status** to **Closed**.

## Your own bank accounts

**Banking > My Bank Accounts** lists the accounts you are paid into. Choose **Add bank account** to add one, or open one to change it. A changed account goes back to unverified until someone checks it. Choose **Close** in an account's menu when you no longer use it. You never see the full number of an account after you save it, and you never see anyone else's accounts.

## Move accounts to another site

Accounts move between sites with **Configuration > Import/Export**: people who manage bank accounts can add the **Bank accounts** step to a deployment plan. The account numbers are left out unless you tick **Include the account numbers**, which only people allowed to see full numbers can do. Keep such a file safe and delete it after the import.
