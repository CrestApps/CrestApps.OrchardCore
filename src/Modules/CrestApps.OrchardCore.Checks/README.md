# Checks

Check printing for any module that pays by check: paychecks, refunds, vendor and agency payments.

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Checks` | Checks | Banking |

- **Check layouts**: blank or pre-printed stock, three sheet forms, the place of every field, calibration, signature images, a second signature above an amount, and the check line (MICR) in a font the site uploads. Files are kept encrypted in the tenant file store, never in the media library. No MICR font ships with the module.
- **Check register**: numbered per bank account through `INumberSequence` (starting at 1001; **Next numbers** for new stock), with the status table Issued, Printed, Voided, StopPayment, Cleared, Reissued and Stale. Voided checks keep their numbers; a reissue gets the next number and links both checks.
- **Printing**: checks with vouchers to one PDF (`ICheckPrinter`, on `CrestApps.OrchardCore.Pdf.Core`), from the register's bulk action **Print with {layout}**.
- **Extension points**: `ICheckVoucherSource` (the stub of a module's checks), `ICheckObserver`, `ICheckMicrFormat` (United States built in), `IAmountInWords` (English built in).
- **Positive pay**: the Check Register data set and the `ChecksPositivePay` recipe's draft pipeline. The module writes no bank file itself.
- Recipe and deployment steps `CheckLayouts` and (opt-in) `CheckRegister`; workflow events Check Issued, Printed, Voided and Cleared.

## Installation

Enable **Checks** in **Tools > Features** (it turns on **Banking**). Add a check layout under **Banking > Check Layouts** and upload a MICR E-13B TrueType font you are licensed to use if the layout prints the check line.

## Usage

```csharp
var result = await checkService.IssueAsync(new CheckIssueRequest
{
    BankAccountId = fundingAccountId,
    PayeeName = "Pat Doe",
    Amount = 812.40m,
    CheckDate = payDate,
    SourceKind = "PayStatement",
    SourceId = statement.ItemId,
});
```

Register an `ICheckVoucherSource` for the source kind to print the stub.

## Documentation

- Technical: `src/CrestApps.Docs/docs/modules/checks.md`
- User manual: `src/CrestApps.Docs/docs/user-manual/finance/checks.md`
