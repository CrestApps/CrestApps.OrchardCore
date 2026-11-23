---
sidebar_label: Checks
title: Checks
description: "Print checks on blank or pre-printed stock, keep the check register, and void, reissue, stop or clear checks."
technical_manual:
  - modules/banking
---

Checks prints the business's checks and keeps a register of every check it wrote: its number, payee, amount and what happened to it. Paychecks, refunds and payments to vendors all go through the same register.

| | |
| --- | --- |
| **Menu** | **Banking** → **Checks**, **Banking** → **Check Layouts** |
| **Permission** | View the check register; Issue and print checks; Manage check layouts |
| **Feature** | Checks (Banking module) |

<AskYourAdmin />

## Set up a check layout

1. Open **Banking** → **Check Layouts** and choose **Add check layout**.
2. Choose the **Paper**: blank check paper, or pre-printed checks from your bank or printer.
3. Choose the **Sheet**: where the check sits on the page, with vouchers above or below it, or three checks a page.
4. On blank paper, fill in the **Payer** name and address that print on the check. Choose the address's country first: the fields follow its format.
5. Under **Signatures**, upload a signature picture if you want one printed, and the amount above which a second signature line prints.
6. Under **Check line (MICR)**, tick **Print the check line** for blank paper and upload the check line font. The font is not included: use a MICR E-13B TrueType font you are licensed to use. Your bank reads the line only when it is printed with magnetic toner.
7. Choose **Save**.

Signature pictures and the font are stored encrypted, never in the media library.

### Arrange the check

The **Check designer** shows the check at its real proportions with sample values, and every change you make on the page shows on it at once: the paper, the payer, the check line and the calibration.

- Drag a field to move it. Drag its right edge to make it wider or narrower.
- Choose a field to edit it in the bar above the check: its **Left** and **Top** place, **Width**, text **Size**, and **Leave off** to stop printing it. The arrow keys move the chosen field one point at a time, ten with Shift.
- A field drawn in red reaches into the band at the bottom that is kept clear for the check line: move it up.
- The payer and the bank show faded on pre-printed checks, since they don't print there; the second signature shows faded until you set an amount for it.
- **Exact positions**, under the check, lists every field's numbers if you prefer to type them.

### Calibrate it

Print a check on plain paper and hold it against the stock. If everything is too far left or up, enter how far to move it under **Calibration**, in points (72 points make an inch). The designer moves with it.

## Write a check

1. Open **Banking** → **Checks** and choose **Write a check**.
2. Choose the **Bank account**: one of the business's checking or savings accounts.
3. Under **Check number**, choose how the check gets its number:
   - **The account's next number**: the check gets it now, ready to print.
   - **Number it when it prints on pre-printed checks**: for checks whose sheets already carry their numbers. The check waits to print without a number, and gets the number of the sheet it prints on. See [Print queued checks](#print-queued-checks).
   - **Already written by hand**: for a check you wrote from a checkbook. Enter the **Number written on the check**. The check is recorded as printed and is never printed here. A number another check of the account has is refused.
4. Enter who it is paid to, the amount, the date and a memo, and choose **Issue**.

Other features can add their own fields to this page, such as the expense accounts a check is posted to, and can refuse a check, such as one dated in a closed accounting period: the page then says why and nothing is written.

Checks that other features write, such as paychecks, appear in the same register.

## Edit a check before it prints

A check that is **Waiting to print** or **Issued** can still be corrected, such as a wrong amount or a misspelled name.

1. In **Banking** → **Checks**, choose **Edit** in the check's menu.
2. Change who it is paid to, the address, the amount, the date or the memo. The bank account and the number can't change.
3. Optionally enter a **Reason**, and choose **Save**.

The feature the check pays can refuse a change, such as a bill payment of more than the bill: the page then says why and nothing changes. Once a check is printed it can't be edited: void it and write a new one instead. When your administrator has turned on the Audit Trail, each edit is recorded with what changed and who changed it.

## Print checks

1. In **Banking** → **Checks**, tick the checks to print.
2. Choose **Print with** and the layout, or **Print with the account's default layout** to use the layout set on the bank account. The checks open as one PDF, and they are marked **Printed**.

Pre-printed checks carry one bank account, so checks of two accounts can't print together on them. A check written by hand is never printed. If you tick only checks that wait for a number, the print run of their account opens.

## Print queued checks

Checks numbered when they print wait in the register as **Waiting to print**. Print them in a run on pre-printed checks:

1. In **Banking** → **Checks**, choose **Print queued checks**, choose the **Bank account** and choose **Show its checks**.
2. Clear any check you want to leave for another run. They print oldest first.
3. Check the **Check layout** (the account's default is chosen for you) and enter the **Number on the first sheet**: the number printed on the top sheet in the printer. Choose **Continue**.
4. Choose **Download the checks** and print them. Nothing is recorded yet.
5. Look at every sheet. Tick each sheet that misprinted, such as a jam, and choose **Record as printed**.

Each check gets the number of its sheet and is marked **Printed**. A misprinted sheet keeps its number as a **Spoiled** check, voided, so the number is never used again, and its check waits for the next run. The account's next number moves past the last sheet. A run is refused when a check of the account already has one of its numbers.

A check that is already printed can't be printed again from the list. If a sheet jams or prints badly:

- Choose **Reprint** in the check's menu, pick the layout and say why, such as "Printer jam". The same check, with the same number, prints again, and the reprint is recorded with your reason.
- Or choose **Reissue**: a new check with the next number replaces it, and the old one is kept as **Reissued**. Use it when the printed check left the office, such as a lost check.

## Load new check stock

When you load a new box of pre-printed checks, choose **Next numbers**, pick the bank account and enter the number printed on the first check. A number a check already has is refused.

## Void a check

Choose **Void** in the check's menu. Enter the **Void date** (today by default) and a reason, and choose **Void**. What the check paid is reversed on that date, such as a bill that is owed again. The date can't be before the check's date. The feature the check pays can refuse, such as for a date in a closed accounting period: the page then says why and nothing changes.

## What happens to a check

| Status | Means |
| --- | --- |
| **Waiting to print** | Written to be numbered when it prints on pre-printed checks. It has no number yet. |
| **Issued** | Written with its number, not printed yet. |
| **Printed** | Printed and given to the payee, or written by hand and recorded. |
| **Cleared** | The bank paid it. Choose **Mark cleared** in its menu. If that was a mistake, such as a bank line matched to the wrong check, choose **Mark not cleared**: it goes back to the status it had before. |
| **Voided** | Cancelled on its void date. It keeps its number. A **Spoiled** check is a sheet that misprinted in a print run. |
| **Stop payment** | You asked the bank not to pay it. Record it with **Stop payment**, and ask the bank too. |
| **Reissued** | Replaced by another check. If the bank pays it anyway, choose **Mark cleared**: the register flags it **Paid after reissue**, so you can check whether the replacement was paid too. |
| **Stale** | Too old for the bank to pay. Your administrator can mark old checks stale every day with the **Mark Stale Checks** workflow task, and start a workflow when a check goes stale. |

## Who sees which checks

Checks written by another feature can be limited to the people who look after them: paychecks, for example, are seen only by people who run that payroll. Checks you write by hand are seen by everyone who may view the register.

## Positive pay

Many banks pay only checks the business told them about. The **Checks: positive pay file** recipe adds a data pipeline under **Tools** → **Data Pipelines** that writes the checks issued, voided or stopped since its last run. Your administrator connects it to your bank and schedules it.
