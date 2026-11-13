---
sidebar_label: Checks
title: Checks
description: "Check printing for any module that pays by check: check layouts with calibration, signatures and the check line (MICR), the check register numbered per bank account, printing to one PDF with vouchers, void, reissue, stop payment, cleared and stale checks, the MICR format and amount-in-words extension points, the voucher source, the Check Register data set and the positive pay pipeline recipe, recipe and deployment steps and workflow events."
user_manual:
  - user-manual/finance/checks
---

The **Checks** module prints checks for any module that pays by check: paychecks first, then agency and vendor payments or refunds. It keeps a check register numbered per bank account and never writes a bank file itself: the bank's positive pay file is a data pipeline over the **Check Register** data set.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Checks` | Checks | [Banking](./banking.md) |

Projects: `CrestApps.OrchardCore.Checks.Abstractions` (models and contracts), `CrestApps.OrchardCore.Checks.Core` (the register store, the rules, the United States check line, English amounts in words, the encrypted file store and the PDF printer on [`CrestApps.OrchardCore.Pdf.Core`](./reports.md)), `CrestApps.OrchardCore.Checks` (the module).

## Check layouts

A `CheckLayout` (a named catalog record, kept in a document) says how checks print:

| Field | Notes |
| --- | --- |
| `Stock` | `Blank` (the layout prints the payer, the bank and the check line) or `PrePrinted`. |
| `Form` | `CheckOnTop`, `CheckInMiddle`, `CheckOnBottom` (one check and two vouchers on a letter sheet) or `ThreePerPage`. |
| `Fields` | The place of each field (`CheckFieldNames`: Date, Payee, PayeeAddress, Amount, AmountInWords, Memo, Number, Payer, Bank, Signature, SecondSignature) in points from the top left of the check: `X`, `Y`, `Width`, `FontSize`, `Hidden`. A new layout starts with places that stay above the check line's clear band (`CheckLayoutDefaults`). |
| `OffsetX`, `OffsetY` | Calibration in points (at most 72 either way). |
| `PrintMicr`, `MicrCountry`, `UseAuxiliaryOnUs`, `MicrFontSize`, `MicrSymbolCharacters` | The check line: on or off, the country format, business (check number in the auxiliary on-us field) or personal format, the font size, and the four characters the font draws the transit, amount, on-us and dash symbols on (`ABCD` in most fonts). |
| `PayerName`, `PayerAddress` | Printed on blank stock. |
| `SecondSignatureAbove` | The amount above which a second signature line prints. |
| `HasSignature`, `HasSecondSignature`, `HasMicrFont` | Whether the files are stored. |

The editor's **Check designer** draws the check at 612 by 264 points with sample values and puts each field where it prints. A field is dragged to move it, its right edge to change its width, and the chosen field is edited in a bar above the check (place, width, text size, left off) or moved with the arrow keys. It shades the check line's clear band and outlines a field that enters it in red, and it follows the paper, payer, check line and calibration as they are typed. The script (`check-layout-designer`, registered by `ChecksResourceManagementOptionsConfiguration`) reads and writes the form's own `Fields[i]` inputs, which stay under **Exact positions**, so the form posts what the designer shows and works without the script.

The signature images (PNG or JPEG, up to 1 MB) and the check line font (TrueType, up to 2 MB) are uploaded on the layout and kept in an encrypted tenant file store (`ICheckFileStore`, `EncryptedFileStore` with the purpose `Checks.Files`, under `App_Data/Sites/{tenant}/Checks`), never in the media library, which serves files to anyone with the URL.

**No check line font ships with the module.** A site uploads a MICR E-13B TrueType font it is licensed to use. Until it does, a layout with **Print the check line** on refuses to print and says why, and the layout list flags it.

## The check register

A `CheckRecord` is one check: `BankAccountId` (one of the business's own accounts, `Owner` empty), `BankAccountName`, `Number`, `Payee` (a `PartyRef`), `PayeeName`, `PayeeAddress` (`Address`), `Amount`, `Currency` (the account's), `CheckDate` (a pure date), `Memo`, `SourceKind` and `SourceId` (what it pays, such as `PayStatement`), `Status`, the dates of each change, `ReissuedById`/`ReissueOfId`, and `IsHistorical` for imported checks. It never holds an account number. Checks are YesSql documents in the `Checks` collection with `CheckRecordIndex`.

`CheckStatuses.Transitions`:

| From | To |
| --- | --- |
| `Issued` | `Printed`, `Voided`, `Reissued` |
| `Printed` | `Voided`, `StopPayment`, `Cleared`, `Reissued`, `Stale` |
| `StopPayment`, `Stale` | `Voided`, `Reissued`, `Cleared` |
| `Voided`, `Cleared`, `Reissued` | none |

Numbers come from `INumberSequence`, one series per bank account (`Checks:{bankAccountId}`), starting at 1001. **Banking > Checks > Next numbers** sets the next number when new stock is loaded and refuses a number a check of the account already has. A voided check keeps its number; a reissue writes a new check with the next number and links both.

## Rules and extension points

`ICheckService` (no permission checks; the caller checks): `IssueAsync`, `GetNextNumberAsync`, `SetNextNumberAsync`, `MarkPrintedAsync`, `VoidAsync`, `ReissueAsync`, `StopPaymentAsync`, `MarkClearedAsync`, `MarkStaleAsync`. Issuing refuses an account another party owns, a closed or failed account, an account without numbers, another currency and an amount that is not more than zero.

| Contract | Register with | Use |
| --- | --- | --- |
| `ICheckVoucherSource` | `AddCheckVoucherSource<T>()` | The voucher (stub) of the checks of one source kind: a title, column headings and rows. Payroll supplies earnings, deductions, taxes and year-to-date totals. Without one, the voucher shows the payee, amount and memo. |
| `ICheckObserver` | `AddCheckObserver<T>()` | Told after each issue, print, void, stop, clear, reissue or stale change. |
| `ICheckMicrFormat` | `AddCheckMicrFormat<T>()` | The check line of one country. The United States format is built in. |
| `IAmountInWords` | `AddAmountInWords<T>()` | The legal line in one language. English is built in ("One thousand two hundred thirty-four and 56/100"). |
| `ICheckPrinter` | built in | Prints checks with a layout to one PDF. |

### The United States check line

`UnitedStatesCheckMicrFormat` places characters by position, counted in 1/8 inch from the right edge of the check, position 1 starting 5/16 inch from the edge (`MicrLine.LeftOf`): the amount field (1 to 12) is left blank for the bank to encode; the account number ends at 15 with the on-us symbol at 14; the routing number sits between transit symbols at 43 and 33; position 44 stays blank; on a business check the check number, on at least six digits, sits between on-us symbols from 45. On a personal check the check number follows the on-us symbol after the account number. The baseline is 1/4 inch above the bottom of the check, inside the 5/8 inch clear band. Reading the account number for the check line is a Banking reveal, so the Audit Trail records each printed check.

## Admin screens and permissions

| Permission | Allows |
| --- | --- |
| `ManageCheckLayouts` | **Banking > Check Layouts**. Security critical. |
| `ManageChecks` | **Write a check**, print (the list's bulk action **Print with {layout}**), void, reissue, stop payment, mark cleared, **Next numbers**. Security critical. |
| `ViewCheckRegister` | **Banking > Checks**, the register, filtered by bank account and status and searched by payee or number. |

A print returns one PDF (`Cache-Control: no-store`) and records the checks as printed in one print run (`PrintRunId`). A spoiled sheet is reissued from the check's menu.

## Positive pay and reports

With Data Pipelines on, the **Checks** data source has a **Check register** data set (requires `ViewCheckRegister`): bank account, number, payee, amount, currency, memo, status, a **Void** flag (voided, stopped or reissued), source, and the check, issue, print, void and cleared dates. The **Checks: positive pay file** recipe (`ChecksPositivePay`) adds a draft pipeline that reads the checks changed since the last successful run (**Read only new rows** on `ModifiedUtc`) and writes a CSV; add the bank's SFTP or web API step, publish it and schedule it with a workflow timer. A bank whose positive pay layout is fixed-width registers an `IDataFileFormat`; the module writes no file itself.

## Recipes and deployment

- `CheckLayouts` (step and deployment step **Check layouts**): layouts by id or name, without files.
- `CheckRegister` (opt-in deployment step **Check register**): every check. Imported checks are history (`IsHistorical`): they keep their numbers and are never printed or changed. A check whose bank account and number already exist is skipped.

Both steps have schemas for CrestApps Recipes.

## Workflows

Events (category Checks): **Check Issued**, **Check Printed**, **Check Voided** (also stop payment and reissue; `Change` says which) and **Check Cleared**. Input: `CheckId`, `Number`, `BankAccountId`, `PayeeName`, `Amount`, `Currency`, `CheckDate`, `SourceKind`, `SourceId`, `Status`, `Change`, `ReplacedById`, `Reason`, `Actor`.
