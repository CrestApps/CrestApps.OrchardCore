---
sidebar_label: Disbursements
title: Disbursements
description: "Send the business's payments in batches: as a bank file (ACH), by printed check, or recorded when paid another way, and follow each one until the bank pays it."
technical_manual:
  - modules/banking
---

Disbursements are the payments the business sends out: pay deposits, vendor payments, refunds. Each one is sent in a batch, as a bank file your bank processes, as printed checks, or recorded when you paid it another way, and you follow it until the bank pays it.

| | |
| --- | --- |
| **Menu** | Banking > Disbursements, Banking > Disbursement Batches |
| **Permission** | View disbursements; Manage disbursements |
| **Feature** | Disbursements (Banking module); Direct Deposit for ACH bank files to US dollar accounts |

<AskYourAdmin />

## Set up bank files

1. Open **Banking > Bank Accounts** and the business's account payments leave from.
2. In **Bank file (ACH)**, tick **Send bank files from this account** and fill in what your bank gave you: company name, company identification, immediate origin, and the bank that receives the file if it isn't this account's bank.
3. Choose **Save**.

## Record a payment

Payments from features such as Payroll appear on their own. To record one by hand:

1. Open **Banking > Disbursements** and choose **Record a disbursement**.
2. Choose how it is sent, the account it leaves from, the amount and, for a bank file, the payee's verified bank account.
3. Choose **Record**.

Recording the same payment twice gives the same disbursement, so a feature that retries never pays twice.

## Send a batch

1. In **Banking > Disbursements**, tick the pending payments and set **Arrives**: the date the money should reach the payees.
2. Choose **Batch to send by** and how they are sent. Payments sent another way, or from another account, stay pending.
3. Open **Banking > Disbursement Batches**, and in the batch's menu choose **Release**.

For a bank file, choose **Download the bank file** and send it to your bank, or let your administrator set up a data pipeline that delivers it. The file holds account numbers: keep it safe. For checks, print them from **Banking > Checks**.

## When the bank pays or returns a payment

- After the bank's return window, choose **Mark paid** in the batch's menu, or have your administrator run the **Settle Disbursement Batches** workflow task every day. Workflows can also start when a batch is made, released, reopened, cancelled or settled, and when a returns file is applied.
- When the bank sends a return file, choose **Import a return file** in **Disbursement Batches**. Returned payments are marked **Returned** with the bank's reason. A notice of change corrects the payee's bank account, which must then be verified again before money goes to it.

## Check a payee's bank account with a test entry

On a payee's bank account, choose **Check with** and **Test entry (prenote)**. A zero-amount entry goes in the next bank file. If the bank doesn't return it, the account becomes **Verified** when its batch is marked paid; if the bank returns it, the account is **Failed**.

## What happens to a payment

| Status | Means |
| --- | --- |
| **Pending** | Recorded, not sent yet. |
| **Batched** | In a batch that isn't released. |
| **Submitted** | In a released bank file, or paid by an issued check. |
| **Settled** | Paid. |
| **Returned** | The bank sent it back. |
| **Failed** | It couldn't be sent, or its check was voided. |
| **Cancelled** | Cancelled before it was sent. |

## Who sees which payments

Payments a feature records can be limited to the people who look after them: pay deposits, for example, are seen only by people who run that payroll. Payments you record by hand are seen by everyone who may view disbursements.
