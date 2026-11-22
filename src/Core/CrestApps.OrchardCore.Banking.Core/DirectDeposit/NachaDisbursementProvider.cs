using System.Text;
using CrestApps.Core;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Numbering;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

using CrestApps.OrchardCore.Banking.Disbursements;
using CrestApps.OrchardCore.Banking.Disbursements.Core;

namespace CrestApps.OrchardCore.Banking.DirectDeposit;

/// <summary>
/// Sends a batch as a bank file (Nacha). The file is stored with the batch; the business downloads it, or a data
/// pipeline delivers it with the Read a disbursement batch file step. Each account number is read once per file, and
/// each read is a Banking reveal the Audit Trail records.
/// </summary>
public sealed class NachaDisbursementProvider : IDisbursementProvider
{
    private readonly IBankAccountManager _bankAccounts;
    private readonly IBankAccountService _bankAccountService;
    private readonly IDisbursementBatchManager _batches;
    private readonly INumberSequence _numbers;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="NachaDisbursementProvider"/> class.
    /// </summary>
    /// <param name="bankAccounts">The bank accounts.</param>
    /// <param name="bankAccountService">The bank account rules, which read account numbers.</param>
    /// <param name="batches">The batches, to number the files of a day.</param>
    /// <param name="numbers">The number sequences, for trace numbers no two files share.</param>
    /// <param name="localClock">The site's clock, for the file's date.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public NachaDisbursementProvider(
        IBankAccountManager bankAccounts,
        IBankAccountService bankAccountService,
        IDisbursementBatchManager batches,
        INumberSequence numbers,
        ILocalClock localClock,
        IClock clock,
        IStringLocalizer<NachaDisbursementProvider> stringLocalizer)
    {
        _bankAccounts = bankAccounts;
        _bankAccountService = bankAccountService;
        _batches = batches;
        _numbers = numbers;
        _localClock = localClock;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name => DisbursementsConstants.Providers.Nacha;

    /// <inheritdoc />
    public LocalizedString DisplayName => S["Bank file (ACH)"];

    /// <inheritdoc />
    public string Method => DisbursementsConstants.Methods.Ach;

    /// <inheritdoc />
    public async Task<DisbursementSubmitResult> SubmitAsync(DisbursementBatch batch, IReadOnlyList<Disbursement> disbursements, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(disbursements);

        var source = string.IsNullOrEmpty(batch.SourceBankAccountId) ? null : await _bankAccounts.FindByIdAsync(batch.SourceBankAccountId, cancellationToken);

        if (source is null)
        {
            return DisbursementSubmitResult.Failed(S["The bank account the money leaves from no longer exists."]);
        }

        if (!BankAccountKinds.IsDeposit(source.Kind))
        {
            return DisbursementSubmitResult.Failed(S["A bank file sends money only from a checking or savings account, and {0} is not one.", source.Name]);
        }

        var settings = source.TryGet<NachaOriginSettings>(out var stored) ? stored : null;

        if (settings is not { Enabled: true } || string.IsNullOrWhiteSpace(settings.ImmediateOrigin) || string.IsNullOrWhiteSpace(settings.CompanyIdentification) || string.IsNullOrWhiteSpace(settings.CompanyName))
        {
            return DisbursementSubmitResult.Failed(S["Set up bank files on {0} first: open the account and fill in Bank file (ACH).", source.Name]);
        }

        if (!string.Equals(batch.Currency, "USD", StringComparison.OrdinalIgnoreCase))
        {
            return DisbursementSubmitResult.Failed(S["A bank file (ACH) pays United States dollars only."]);
        }

        if (string.IsNullOrWhiteSpace(batch.EntryDescription) && string.IsNullOrWhiteSpace(settings.CompanyEntryDescription))
        {
            return DisbursementSubmitResult.Failed(S["Enter the company entry description in Bank file (ACH) on {0}, such as PAYROLL.", source.Name]);
        }

        if (batch.EffectiveDate < DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime))
        {
            return DisbursementSubmitResult.Failed(S["The arrival date of the batch is in the past. Cancel the batch and batch its disbursements again with a later date."]);
        }

        var reason = S["Written to the bank file of batch {0}", batch.ItemId].Value;
        var numbers = new Dictionary<string, string>(StringComparer.Ordinal);
        var accounts = new Dictionary<string, BankAccount>(StringComparer.Ordinal);
        var errors = new List<string>();

        async Task<string> NumberOfAsync(BankAccount account)
        {
            if (!numbers.TryGetValue(account.ItemId, out var number))
            {
                number = await _bankAccountService.RevealAccountNumberAsync(account, reason, cancellationToken);
                numbers[account.ItemId] = number;
            }

            return number;
        }

        var request = new NachaFileRequest
        {
            Settings = settings,
            OriginRoutingNumber = source.RoutingNumber,
            OriginIsSavings = source.Kind == BankAccountKind.Savings,
            CreatedLocal = (await _localClock.GetLocalNowAsync()).DateTime,
            EffectiveDate = batch.EffectiveDate,
            EntryDescription = batch.EntryDescription,
        };

        if (settings.IncludeOffset)
        {
            request.OriginAccountNumber = await NumberOfAsync(source);
        }

        foreach (var disbursement in disbursements)
        {
            if (!accounts.TryGetValue(disbursement.DestinationBankAccountId ?? string.Empty, out var destination))
            {
                destination = string.IsNullOrEmpty(disbursement.DestinationBankAccountId) ? null : await _bankAccounts.FindByIdAsync(disbursement.DestinationBankAccountId, cancellationToken);

                if (destination is not null)
                {
                    accounts[destination.ItemId] = destination;
                }
            }

            if (destination is null || !BankAccountNumbers.HasNumbers(destination))
            {
                errors.Add(S["The bank account of {0} no longer exists or has no numbers.", disbursement.PayeeName]);

                continue;
            }

            if (!BankAccountKinds.IsDeposit(destination.Kind))
            {
                errors.Add(S["The bank account of {0} is not a checking or savings account, which a bank file pays into.", disbursement.PayeeName]);

                continue;
            }

            if (!disbursement.IsPrenote && !destination.CanReceivePayments())
            {
                errors.Add(S["The bank account of {0} is no longer verified.", disbursement.PayeeName]);

                continue;
            }

            var number = await NumberOfAsync(destination);

            if (string.IsNullOrEmpty(number))
            {
                errors.Add(S["The account number of {0} can't be read on this site.", disbursement.PayeeName]);

                continue;
            }

            if (number.Replace(" ", string.Empty, StringComparison.Ordinal).Length > NachaFileWriter.MaxAccountNumberLength)
            {
                errors.Add(S["The account number of {0} is longer than {1} characters, which a bank file can't hold. Pay it another way.", disbursement.PayeeName, NachaFileWriter.MaxAccountNumberLength]);

                continue;
            }

            request.Entries.Add(new NachaEntry
            {
                Key = disbursement.ItemId,
                IsBusiness = disbursement.IsBusinessPayee,
                IsPrenote = disbursement.IsPrenote,
                IsSavings = destination.Kind == BankAccountKind.Savings,
                RoutingNumber = destination.RoutingNumber,
                AccountNumber = number,
                AmountCents = CurrencyScale.ToMinorUnits(disbursement.Amount, disbursement.Currency),
                IndividualId = disbursement.Payee.IsEmpty ? disbursement.ItemId : disbursement.Payee.Id,
                Name = disbursement.PayeeName,
            });
        }

        if (errors.Count > 0)
        {
            return DisbursementSubmitResult.Failed([.. errors]);
        }

        // A return names its entry by trace number alone, so each comes from a series of the originating bank.
        var series = "Disbursements:Trace:" + new string((source.RoutingNumber ?? string.Empty).Where(char.IsAsciiDigit).Take(8).ToArray());

        for (var index = 0; index < request.Entries.Count + (settings.IncludeOffset ? 1 : 0); index++)
        {
            request.TraceSequences.Add(await _numbers.NextAsync(series, cancellationToken: cancellationToken));
        }

        // The files of the business's day, in the site's time zone, are lettered A, B, C...
        var dayStartUtc = await _localClock.ConvertToUtcAsync(request.CreatedLocal.Date);
        request.FileIdModifier = NachaFileWriter.FileIdModifier(await _batches.CountReleasedAsync(source.ItemId, dayStartUtc, _clock.UtcNow, cancellationToken));

        NachaFileResult file;

        try
        {
            file = NachaFileWriter.Write(request);
        }
        catch (ArgumentException)
        {
            return DisbursementSubmitResult.Failed(S["A routing or account number of the batch doesn't fit the bank file. Check the bank accounts."]);
        }

        var result = new DisbursementSubmitResult
        {
            Succeeded = true,
            File = Encoding.ASCII.GetBytes(file.Text),
            FileName = $"ach-{request.CreatedLocal:yyyyMMdd-HHmm}-{request.FileIdModifier}.txt",
        };

        foreach (var (key, trace) in file.TraceNumbers)
        {
            result.TraceNumbers[key] = trace;
            result.Statuses[key] = DisbursementStatus.Submitted;
        }

        return result;
    }
}
