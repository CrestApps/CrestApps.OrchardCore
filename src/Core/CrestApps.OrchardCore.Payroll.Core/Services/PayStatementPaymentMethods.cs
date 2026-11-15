using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Disbursements;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Staff;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// The source kind of disbursements and checks that pay pay statements.
/// </summary>
public static class PayStatementSources
{
    /// <summary>The source kind: a pay statement, scoped by its employer.</summary>
    public const string Kind = "PayStatement";
}

/// <summary>
/// Direct Deposit: a deposit row of net pay is paid by an ACH disbursement from the employer's funding account to the
/// person's bank account, once per payment (its idempotency key is the statement and the payment's place).
/// </summary>
public sealed class DirectDepositPaymentMethod : IPayStatementPaymentMethod
{
    private readonly IDisbursementService _disbursements;
    private readonly IDisbursementManager _manager;
    private readonly IBankAccountManager _accounts;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectDepositPaymentMethod"/> class.
    /// </summary>
    /// <param name="disbursements">The disbursement service.</param>
    /// <param name="manager">The disbursements.</param>
    /// <param name="accounts">The bank accounts, for the name of the account paid.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DirectDepositPaymentMethod(IDisbursementService disbursements, IDisbursementManager manager, IBankAccountManager accounts, IStringLocalizer<DirectDepositPaymentMethod> stringLocalizer)
    {
        _disbursements = disbursements;
        _manager = manager;
        _accounts = accounts;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public NetPayMethod Method => NetPayMethod.BankAccount;

    /// <inheritdoc />
    public async Task<PayStatementPaymentResult> PayAsync(PayStatementPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = new PayStatementPaymentResult();

        if (string.IsNullOrEmpty(context.Employer?.FundingBankAccountId))
        {
            result.Errors.Add(S["The employer has no account to pay from: choose one under its Funding account."]);

            return result;
        }

        var created = await _disbursements.CreateAsync(new DisbursementRequest
        {
            Payee = new PartyRef(StaffConstants.PartyKind, context.Statement.StaffMemberId),
            PayeeName = context.Statement.Snapshot?.EmployeeName,
            PayeeAddress = context.Profile?.HomeAddress,
            Amount = context.Payment.Amount,
            Currency = context.Statement.Currency,
            Method = DisbursementsConstants.Methods.Ach,
            SourceBankAccountId = context.Employer.FundingBankAccountId,
            DestinationBankAccountId = context.Payment.BankAccountId,
            SourceKind = PayStatementSources.Kind,
            SourceId = context.Statement.ItemId,
            AccessScope = context.Statement.EmployerId,
            Memo = S["Pay {0}", context.Statement.PayDate.ToString("d", CultureInfo.CurrentCulture)],
            EffectiveDate = context.Statement.PayDate,
            IdempotencyKey = context.Payment.Attempt > 0 ? $"payroll:{context.Statement.ItemId}:{context.Index}:{context.Payment.Attempt}" : $"payroll:{context.Statement.ItemId}:{context.Index}",
        }, cancellationToken);

        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                result.Errors.Add(error);
            }

            return result;
        }

        result.ReferenceId = created.Disbursement.ItemId;
        result.Reference = await _accounts.FindByIdAsync(context.Payment.BankAccountId, cancellationToken) is { } account
            ? S["to {0}", account.Name]
            : null;

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> VoidAsync(PayStatementPaymentContext context, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return !string.IsNullOrEmpty(context.Payment.ReferenceId)
            && await _manager.FindByIdAsync(context.Payment.ReferenceId, cancellationToken) is { } disbursement
            && await _disbursements.CancelAsync(disbursement, reason, cancellationToken);
    }
}

/// <summary>
/// Paychecks: a check row of net pay, or one left over, is printed as a check on the employer's funding account, with the
/// statement as its voucher.
/// </summary>
public sealed class PaycheckPaymentMethod : IPayStatementPaymentMethod
{
    private readonly ICheckService _checks;
    private readonly ICheckManager _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaycheckPaymentMethod"/> class.
    /// </summary>
    /// <param name="checks">The check service.</param>
    /// <param name="manager">The checks.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaycheckPaymentMethod(ICheckService checks, ICheckManager manager, IStringLocalizer<PaycheckPaymentMethod> stringLocalizer)
    {
        _checks = checks;
        _manager = manager;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public NetPayMethod Method => NetPayMethod.Check;

    /// <inheritdoc />
    public async Task<PayStatementPaymentResult> PayAsync(PayStatementPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = new PayStatementPaymentResult();

        if (string.IsNullOrEmpty(context.Employer?.FundingBankAccountId))
        {
            result.Errors.Add(S["The employer has no account to pay from: choose one under its Funding account."]);

            return result;
        }

        var issued = await _checks.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = context.Employer.FundingBankAccountId,
            Payee = new PartyRef(StaffConstants.PartyKind, context.Statement.StaffMemberId),
            PayeeName = context.Statement.Snapshot?.EmployeeName,
            PayeeAddress = context.Profile?.HomeAddress,
            Amount = context.Payment.Amount,
            Currency = context.Statement.Currency,
            CheckDate = context.Statement.PayDate,
            Memo = S["Pay {0}", context.Statement.PayDate.ToString("d", CultureInfo.CurrentCulture)],
            SourceKind = PayStatementSources.Kind,
            SourceId = context.Statement.ItemId,
            AccessScope = context.Statement.EmployerId,
        }, cancellationToken);

        if (!issued.Succeeded)
        {
            foreach (var error in issued.Errors)
            {
                result.Errors.Add(error);
            }

            return result;
        }

        result.ReferenceId = issued.Check.ItemId;
        result.Reference = S["Check {0}", issued.Check.Number];

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> VoidAsync(PayStatementPaymentContext context, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return !string.IsNullOrEmpty(context.Payment.ReferenceId)
            && await _manager.FindByIdAsync(context.Payment.ReferenceId, cancellationToken) is { } check
            && await _checks.VoidAsync(check, reason, cancellationToken);
    }
}

/// <summary>
/// Limits who sees and works the disbursements and checks that pay statements to the employers they may see or process
/// the pay runs of.
/// </summary>
public sealed class PayStatementSourceAccess : ISourceAccess
{
    private readonly IPayrollScopeService _scope;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayStatementSourceAccess"/> class.
    /// </summary>
    /// <param name="scope">The payroll scope.</param>
    public PayStatementSourceAccess(IPayrollScopeService scope)
    {
        _scope = scope;
    }

    /// <inheritdoc />
    public string SourceKind => PayStatementSources.Kind;

    /// <inheritdoc />
    public async Task<SourceAccessScope> GetScopeAsync(ClaimsPrincipal user, SourceAccessLevel level, CancellationToken cancellationToken = default)
    {
        var scope = await _scope.GetScopeAsync(user, level == SourceAccessLevel.Manage ? PayrollPermissions.ProcessPayRuns : PayrollPermissions.ViewPayRuns, cancellationToken);

        return scope.All ? SourceAccessScope.All : SourceAccessScope.Of(scope.EmployerIds);
    }
}

/// <summary>
/// Prints the pay statement as the voucher of the check that pays it: each line with its hours, the current amount and
/// the year's, then gross and net pay.
/// </summary>
public sealed class PayStatementCheckVoucherSource : ICheckVoucherSource
{
    private readonly PayStatementStore _statements;
    private readonly IPayAccumulatorService _accumulators;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayStatementCheckVoucherSource"/> class.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="accumulators">The accumulators, for the year's amounts.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayStatementCheckVoucherSource(PayStatementStore statements, IPayAccumulatorService accumulators, IStringLocalizer<PayStatementCheckVoucherSource> stringLocalizer)
    {
        _statements = statements;
        _accumulators = accumulators;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string SourceKind => PayStatementSources.Kind;

    /// <inheritdoc />
    public async Task<CheckVoucher> GetVoucherAsync(CheckRecord check, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (string.IsNullOrEmpty(check.SourceId) || await _statements.FindByIdAsync(check.SourceId, cancellationToken) is not { } statement)
        {
            return null;
        }

        var accumulators = await _accumulators.GetAsync(statement.StaffMemberId, statement.EmployerId, statement.PayDate, statement.PeriodStart, statement.PeriodEnd, cancellationToken);
        var lines = new List<CheckVoucherLine>();

        string Money(decimal value)
            => string.IsNullOrEmpty(statement.Currency) ? value.ToString("N2", CultureInfo.InvariantCulture) : CurrencyScale.Format(value, statement.Currency);

        foreach (var line in statement.Lines.Where(line => line.Type != PayLineType.Memo))
        {
            var key = line.Type == PayLineType.Tax ? PayAccumulators.TaxKey(line.TaxCode) : PayAccumulators.CodeKey(line.PayCodeId);
            var sign = line.Type is PayLineType.Deduction or PayLineType.Tax && line.Payer == PayLinePayer.Employee ? -1 : 1;

            if (line.Payer == PayLinePayer.Employer && line.Type != PayLineType.Earning)
            {
                continue;
            }

            lines.Add(new CheckVoucherLine
            {
                Label = string.IsNullOrEmpty(line.Description) ? line.TaxCode : line.Description,
                Values =
                [
                    line.Hours is { } hours ? hours.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty,
                    Money(sign * line.Amount),
                    Money(sign * accumulators.For(key).Year),
                ],
            });
        }

        lines.Add(new CheckVoucherLine { Label = S["Gross pay"], Values = [string.Empty, Money(statement.Gross), string.Empty], IsEmphasized = true });
        lines.Add(new CheckVoucherLine { Label = S["Net pay"], Values = [string.Empty, Money(statement.Net), string.Empty], IsEmphasized = true });

        // Net pay split between payments: each one, so the check's amount adds up.
        if (statement.Payments.Count > 1)
        {
            foreach (var payment in statement.Payments)
            {
                var label = payment.ReferenceId == check.ItemId
                    ? S["This check"].Value
                    : payment.Method == NetPayMethod.BankAccount
                        ? string.IsNullOrEmpty(payment.Reference) ? S["Deposit"].Value : S["Deposit {0}", payment.Reference].Value
                        : payment.Reference ?? S["Check"].Value;

                lines.Add(new CheckVoucherLine { Label = label, Values = [string.Empty, Money(payment.Amount), string.Empty] });
            }
        }

        return new CheckVoucher
        {
            Title = S["Pay statement {0}, period {1} to {2}", statement.Number, statement.PeriodStart.ToString("d", CultureInfo.CurrentCulture), statement.PeriodEnd.ToString("d", CultureInfo.CurrentCulture)],
            Columns = [S["Pay"], S["Hours"], S["Current"], S["Year to date"]],
            Lines = lines,
        };
    }
}
