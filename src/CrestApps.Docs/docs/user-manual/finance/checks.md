---
sidebar_label: Checks
title: Checks
description: "Print checks on blank or pre-printed stock, keep the check register, and void, reissue, stop or clear checks."
technical_manual:
  - modules/checks
---

Checks prints the business's checks and keeps a register of every check it wrote: its number, payee, amount and what happened to it. Paychecks, refunds and payments to vendors all go through the same register.

| | |
| --- | --- |
| **Menu** | Banking > Checks, Banking > Check Layouts |
| **Permission** | View the check register; Issue and print checks; Manage check layouts |
| **Feature** | Checks |

<AskYourAdmin />

## Set up a check layout

1. Open **Banking > Check Layouts** and choose **Add check layout**.
2. Choose the **Paper**: blank check paper, or pre-printed checks from your bank or printer.
3. Choose the **Sheet**: where the check sits on the page, with vouchers above or below it, or three checks a page.
4. On blank paper, fill in the **Payer** name and address that print on the check.
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

1. Open **Banking > Checks** and choose **Write a check**.
2. Choose the **Bank account**, enter who it is paid to, the amount, the date and a memo, and choose **Issue**.

The check gets the account's next number. Checks that other features write, such as paychecks, appear in the same register.

## Print checks

1. In **Banking > Checks**, tick the checks to print.
2. Choose **Print with** and the layout. The checks open as one PDF, and they are marked **Printed**.

If a sheet jams or prints badly, open the check's menu and choose **Reissue**: a new check with the next number replaces it, and the old one is kept as **Reissued**.

## Load new check stock

When you load a new box of pre-printed checks, choose **Next numbers**, pick the bank account and enter the number printed on the first check. A number a check already has is refused.

## What happens to a check

| Status | Means |
| --- | --- |
| **Issued** | Written, not printed yet. |
| **Printed** | Printed and given to the payee. |
| **Cleared** | The bank paid it. Choose **Mark cleared** in its menu. |
| **Voided** | Cancelled. It keeps its number. |
| **Stop payment** | You asked the bank not to pay it. Record it with **Stop payment**, and ask the bank too. |
| **Reissued** | Replaced by another check. |
| **Stale** | Too old for the bank to pay. |

## Positive pay

Many banks pay only checks the business told them about. The **Checks: positive pay file** recipe adds a data pipeline under **Tools > Data Pipelines** that writes the checks issued, voided or stopped since its last run. Your administrator connects it to your bank and schedules it.
