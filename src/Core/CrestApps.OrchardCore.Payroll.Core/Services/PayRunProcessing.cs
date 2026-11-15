using System.Globalization;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Processing, voiding and reissuing: the part of the pay run service that makes statements final and pays them.
/// </summary>
/// <remarks>
/// Disbursements, Checks and the pay input sources commit the session on their own. A commit makes the session forget
/// what it read before, and saving such a record afterwards inserts it again, so every record is read again by its id
/// after a call that may commit, and changed only then.
/// </remarks>
public sealed partial class DefaultPayRunService
{
    /// <inheritdoc />
    public async Task<PayRunResult> ProcessAsync(PayRun run, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var result = new PayRunResult();

        // Processing is also allowed again: the run's lock means a run left processing was stopped half-way.
        if (run.Status is not (PayRunStatus.Approved or PayRunStatus.PartiallyPaid or PayRunStatus.Failed or PayRunStatus.Processing))
        {
            result.Errors.Add(S["Only an approved pay run is processed, or one that was partly paid to pay what is left."]);

            return result;
        }

        var (locker, locked) = await _locks.TryAcquireLockAsync("Payroll:PayRun:" + run.ItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Errors.Add(S["The pay run is being worked on. Try again in a moment."]);

            return result;
        }

        await using (locker)
        {
            var runId = run.ItemId;
            var employer = await _employers.FindByIdAsync(run.EmployerId, cancellationToken);
            var methods = PaymentMethods();
            var numbers = _services.GetRequiredService<INumberSequence>();
            var items = _services.GetRequiredService<IPayItemManager>();
            var codes = _services.GetRequiredService<PayCodeStore>();
            var issued = new List<string>();
            var sources = new List<PayInputReference>();

            // First, nothing that commits: drafts become final and numbered, pay items move, and the run is processing.
            foreach (var statement in await OpenStatementsAsync(run, cancellationToken))
            {
                if (statement.Status != PayStatementStatus.Draft)
                {
                    continue;
                }

                var profile = await _profiles.FindByIdAsync(statement.ProfileId, cancellationToken);

                statement.Number = (await numbers.NextAsync(PayrollConstants.PayStatementNumberSeries, cancellationToken: cancellationToken)).ToString(CultureInfo.InvariantCulture);
                statement.Status = PayStatementStatus.Final;
                statement.ProcessedUtc = _clock.UtcNow;
                statement.ModifiedUtc = _clock.UtcNow;
                statement.Snapshot.EmployeeAddress = await AddressLinesAsync(profile?.HomeAddress);
                statement.Snapshot.EmployerAddress = await AddressLinesAsync(employer?.Address);

                await MovePayItemsAsync(statement, items, codes, forward: true, cancellationToken);
                await _statements.UpdateAsync(statement, cancellationToken);
                sources.AddRange(statement.Lines.SelectMany(line => line.Sources));
                issued.Add(statement.ItemId);
            }

            run.Status = PayRunStatus.Processing;
            await _runs.UpdateAsync(run, cancellationToken: cancellationToken);

            // Then the sources and the payments, each of which may commit.
            if (sources.Count > 0)
            {
                await _services.GetRequiredService<IPayInputService>().MarkConsumedAsync(new PayInputConsumption
                {
                    ConsumerId = PayrollConstants.PayRunConsumerPrefix + runId,
                    Sources = [.. sources.Distinct()],
                }, cancellationToken);
            }

            foreach (var statementId in (await OpenStatementsAsync(await FreshRunAsync(runId, run, cancellationToken), cancellationToken)).Where(statement => statement.Status == PayStatementStatus.Final).Select(statement => statement.ItemId).ToList())
            {
                var count = (await _statements.FindByIdAsync(statementId, cancellationToken)).Payments.Count;

                for (var index = 0; index < count; index++)
                {
                    var statement = await _statements.FindByIdAsync(statementId, cancellationToken);
                    var payment = statement.Payments[index];

                    if (!string.IsNullOrEmpty(payment.ReferenceId) || payment.Amount <= 0)
                    {
                        continue;
                    }

                    var problem = await PayAndSaveAsync(methods, statementId, index, employer, cancellationToken);

                    if (problem is not null)
                    {
                        result.Errors.Add(S["{0}: {1}", statement.Snapshot?.EmployeeName ?? statement.StaffMemberId, problem]);
                    }
                }
            }

            var statements = await OpenStatementsAsync(await FreshRunAsync(runId, run, cancellationToken), cancellationToken);

            // A statement with nothing left to pay, such as a net pay of zero, is paid.
            foreach (var statement in statements.Where(statement => statement.Status == PayStatementStatus.Final && statement.Payments.All(payment => !string.IsNullOrEmpty(payment.ReferenceId) || payment.Amount <= 0)))
            {
                statement.Status = PayStatementStatus.Paid;
                statement.ModifiedUtc = _clock.UtcNow;
                await _statements.UpdateAsync(statement, cancellationToken);
            }

            var paid = statements.Count(statement => statement.Status == PayStatementStatus.Paid);
            var fresh = await FreshRunAsync(runId, run, cancellationToken);

            fresh.Status = paid == statements.Count ? PayRunStatus.Paid : paid == 0 && issued.Count == 0 ? PayRunStatus.Failed : PayRunStatus.PartiallyPaid;
            fresh.ProcessedUtc = _clock.UtcNow;
            fresh.ProcessedBy = user?.Identity?.Name;
            fresh.Totals = Totals(statements);
            await _runs.UpdateAsync(fresh, cancellationToken: cancellationToken);

            foreach (var observer in _services.GetServices<IPayStatementObserver>())
            {
                foreach (var statementId in issued)
                {
                    await observer.IssuedAsync(await _statements.FindByIdAsync(statementId, cancellationToken), cancellationToken);
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PayRunResult> VoidStatementAsync(PayStatement statement, ClaimsPrincipal user, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var result = new PayRunResult();

        if (statement.Status is not (PayStatementStatus.Final or PayStatementStatus.Paid) || statement.VoidOf is not null)
        {
            result.Errors.Add(S["Only a final pay statement can be voided."]);

            return result;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            result.Errors.Add(S["Say why the statement is voided."]);

            return result;
        }

        var (locker, locked) = await _locks.TryAcquireLockAsync("Payroll:PayStatement:" + statement.ItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Errors.Add(S["The pay statement is being worked on. Try again in a moment."]);

            return result;
        }

        await using (locker)
        {
            var statementId = statement.ItemId;
            var profile = await _profiles.FindByIdAsync(statement.ProfileId, cancellationToken);
            var employer = await _employers.FindByIdAsync(statement.EmployerId, cancellationToken);
            var methods = PaymentMethods();

            // First, nothing that commits: the reversing statement, the pay items moved back, the original voided.
            var reversal = new PayStatement
            {
                ItemId = UniqueId.GenerateId(),
                Number = (await _services.GetRequiredService<INumberSequence>().NextAsync(PayrollConstants.PayStatementNumberSeries, cancellationToken: cancellationToken)).ToString(CultureInfo.InvariantCulture),
                RunId = statement.RunId,
                ProfileId = statement.ProfileId,
                StaffMemberId = statement.StaffMemberId,
                EmployerId = statement.EmployerId,
                PayDate = statement.PayDate,
                PeriodStart = statement.PeriodStart,
                PeriodEnd = statement.PeriodEnd,
                Currency = statement.Currency,
                Gross = -statement.Gross,
                Net = -statement.Net,
                Lines = [.. statement.Lines.Select(Reverse)],
                Warnings = [reason],
                VoidOf = statement.ItemId,
                Snapshot = statement.Snapshot,
                Status = PayStatementStatus.Final,
                CreatedUtc = _clock.UtcNow,
                ProcessedUtc = _clock.UtcNow,
                ModifiedUtc = _clock.UtcNow,
            };

            await _statements.CreateAsync(reversal, cancellationToken);
            await MovePayItemsAsync(statement, _services.GetRequiredService<IPayItemManager>(), _services.GetRequiredService<PayCodeStore>(), forward: false, cancellationToken);

            statement.Status = PayStatementStatus.Voided;
            statement.VoidedBy = reversal.ItemId;
            statement.ModifiedUtc = _clock.UtcNow;
            await _statements.UpdateAsync(statement, cancellationToken);

            if (await _runs.FindByIdAsync(statement.RunId, cancellationToken) is { } run)
            {
                run.Totals = Totals(await OpenStatementsAsync(run, cancellationToken));
                await _runs.UpdateAsync(run, cancellationToken: cancellationToken);
            }

            // Then the sources and the payments, which may commit. Payments that went out (a deposit sent to the bank,
            // a check cashed) can't be stopped: say so; the void still records that the pay is owed back.
            var sources = statement.Lines.SelectMany(line => line.Sources).Distinct().ToList();

            if (sources.Count > 0)
            {
                await _services.GetRequiredService<IPayInputService>().ReleaseAsync(new PayInputConsumption
                {
                    ConsumerId = PayrollConstants.PayRunConsumerPrefix + statement.RunId,
                    Sources = sources,
                }, cancellationToken);
            }

            for (var index = 0; index < statement.Payments.Count; index++)
            {
                var current = await _statements.FindByIdAsync(statementId, cancellationToken);
                var payment = current.Payments[index];

                if (string.IsNullOrEmpty(payment.ReferenceId) || !methods.TryGetValue(payment.Method, out var method))
                {
                    continue;
                }

                var context = new PayStatementPaymentContext { Statement = current, Payment = payment, Index = index, Profile = profile, Employer = employer };

                if (!await method.VoidAsync(context, reason, cancellationToken))
                {
                    result.Errors.Add(S["The payment {0} could not be stopped, because it is in a bank file or was cashed: recover it from the person.", payment.Reference ?? payment.ReferenceId]);
                }
            }

            var voided = await _statements.FindByIdAsync(statementId, cancellationToken);
            var created = await _statements.FindByIdAsync(reversal.ItemId, cancellationToken);

            foreach (var observer in _services.GetServices<IPayStatementObserver>())
            {
                await observer.VoidedAsync(voided, created, cancellationToken);
            }

            result.Statement = created;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PayRunResult> ReissuePaymentAsync(PayStatement statement, int index, ClaimsPrincipal user, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var result = new PayRunResult();

        if (statement.Status is not (PayStatementStatus.Final or PayStatementStatus.Paid) || statement.VoidOf is not null || index < 0 || index >= statement.Payments.Count)
        {
            result.Errors.Add(S["Only a payment of a final pay statement can be reissued."]);

            return result;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            result.Errors.Add(S["Say why the payment is reissued."]);

            return result;
        }

        var (locker, locked) = await _locks.TryAcquireLockAsync("Payroll:PayStatement:" + statement.ItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Errors.Add(S["The pay statement is being worked on. Try again in a moment."]);

            return result;
        }

        await using (locker)
        {
            var statementId = statement.ItemId;
            var profile = await _profiles.FindByIdAsync(statement.ProfileId, cancellationToken);
            var employer = await _employers.FindByIdAsync(statement.EmployerId, cancellationToken);
            var methods = PaymentMethods();
            var payment = statement.Payments[index];

            if (!string.IsNullOrEmpty(payment.ReferenceId) && methods.TryGetValue(payment.Method, out var current)
                && !await current.VoidAsync(new PayStatementPaymentContext { Statement = statement, Payment = payment, Index = index, Profile = profile, Employer = employer }, reason, cancellationToken))
            {
                result.Errors.Add(S["The payment {0} could not be stopped, because it is in a bank file or was cashed, so it is not paid again.", payment.Reference ?? payment.ReferenceId]);

                return result;
            }

            // The old payment is stopped: the next attempt has its own idempotency key.
            var stopped = await _statements.FindByIdAsync(statementId, cancellationToken);

            stopped.Payments[index].ReferenceId = null;
            stopped.Payments[index].Reference = null;
            stopped.Payments[index].Attempt++;
            stopped.Status = PayStatementStatus.Final;
            stopped.ModifiedUtc = _clock.UtcNow;
            await _statements.UpdateAsync(stopped, cancellationToken);

            if (await PayAndSaveAsync(methods, statementId, index, employer, cancellationToken) is { } problem)
            {
                result.Errors.Add(problem);
            }
        }

        return result;
    }

    // Pays one payment with the feature of its method (a deposit with no feature to pay it is paid by check when
    // Paychecks is on), then reads the statement again, keeps the reference or the problem on the payment, and marks the
    // statement paid once every payment is. Returns the problem, or null.
    private async Task<string> PayAndSaveAsync(Dictionary<NetPayMethod, IPayStatementPaymentMethod> methods, string statementId, int index, Employer employer, CancellationToken cancellationToken)
    {
        var statement = await _statements.FindByIdAsync(statementId, cancellationToken);
        var payment = statement.Payments[index];
        var method = payment.Method;

        if (!methods.TryGetValue(method, out var payer) && method == NetPayMethod.BankAccount && methods.TryGetValue(NetPayMethod.Check, out payer))
        {
            method = NetPayMethod.Check;
        }

        PayStatementPaymentResult outcome = null;
        string problem;

        if (payer is null)
        {
            problem = method == NetPayMethod.Check ? S["Turn on Paychecks to pay by check."] : S["Turn on Direct Deposit to pay to a bank account."];
        }
        else
        {
            var profile = await _profiles.FindByIdAsync(statement.ProfileId, cancellationToken);

            outcome = await payer.PayAsync(new PayStatementPaymentContext { Statement = statement, Payment = payment, Index = index, Profile = profile, Employer = employer }, cancellationToken);
            problem = outcome.Succeeded ? null : outcome.Errors.Count > 0 ? string.Join(" ", outcome.Errors) : S["The payment could not be made."];
        }

        var fresh = await _statements.FindByIdAsync(statementId, cancellationToken);
        var saved = fresh.Payments[index];

        saved.Method = method;
        saved.Problem = problem;

        if (outcome?.Succeeded == true)
        {
            saved.ReferenceId = outcome.ReferenceId;
            saved.Reference = outcome.Reference;
        }

        if (fresh.Payments.All(candidate => !string.IsNullOrEmpty(candidate.ReferenceId) || candidate.Amount <= 0))
        {
            fresh.Status = PayStatementStatus.Paid;
        }

        fresh.ModifiedUtc = _clock.UtcNow;
        await _statements.UpdateAsync(fresh, cancellationToken);

        return problem;
    }

    // The statements of a run that count: neither voided nor reversing.
    private async Task<List<PayStatement>> OpenStatementsAsync(PayRun run, CancellationToken cancellationToken)
        => [.. (await ListStatementsAsync(run, cancellationToken)).Where(statement => statement.VoidOf is null && statement.Status != PayStatementStatus.Voided)];

    private async Task<PayRun> FreshRunAsync(string runId, PayRun fallback, CancellationToken cancellationToken)
        => await _runs.FindByIdAsync(runId, cancellationToken) ?? fallback;

    // A pay item moves toward its goal by what its lines took, and keeps what a cut left owed; a void moves it back.
    private async Task MovePayItemsAsync(PayStatement statement, IPayItemManager items, PayCodeStore codes, bool forward, CancellationToken cancellationToken)
    {
        foreach (var group in statement.Lines.Where(line => line.PayItemId is not null && line.Type != PayLineType.EmployerContribution).GroupBy(line => line.PayItemId, StringComparer.Ordinal))
        {
            if (await items.FindByIdAsync(group.Key, cancellationToken) is not { } item)
            {
                continue;
            }

            var taken = group.Sum(line => line.Amount);

            if (item.Goal is not null)
            {
                item.PaidTowardGoal = Math.Max(0, item.PaidTowardGoal + (forward ? taken : -taken));
            }

            var carries = await codes.FindByIdAsync(item.PayCodeId, cancellationToken) is { } code && code.GetPayrollSettings().Arrears == PayCodeArrearsRule.CarryForward;

            if (carries)
            {
                foreach (var line in group)
                {
                    if (forward)
                    {
                        line.ArrearsBefore = item.ArrearsBalance;
                        item.ArrearsBalance = line.ArrearsAdded;
                    }
                    else if (line.ArrearsBefore is { } before)
                    {
                        item.ArrearsBalance = before;
                    }
                }
            }

            item.ModifiedUtc = _clock.UtcNow;
            await items.UpdateAsync(item, cancellationToken: cancellationToken);
        }
    }

    private Dictionary<NetPayMethod, IPayStatementPaymentMethod> PaymentMethods()
    {
        var methods = new Dictionary<NetPayMethod, IPayStatementPaymentMethod>();

        foreach (var method in _services.GetServices<IPayStatementPaymentMethod>())
        {
            methods.TryAdd(method.Method, method);
        }

        return methods;
    }

    private static PayStatementLine Reverse(PayStatementLine line)
        => new()
        {
            Type = line.Type,
            PayCodeId = line.PayCodeId,
            TaxCode = line.TaxCode,
            TaxCategory = line.TaxCategory,
            Description = line.Description,
            Hours = -line.Hours,
            Rate = line.Rate,
            Amount = -line.Amount,
            Jurisdiction = line.Jurisdiction,
            Payer = line.Payer,
            TaxableWages = -line.TaxableWages,
            PayItemId = line.PayItemId,
            Allocation = line.Allocation,
        };

    private async Task<IList<string>> AddressLinesAsync(CrestApps.OrchardCore.Addresses.Models.Address address)
    {
        if (address is null)
        {
            return [];
        }

        if (_services.GetService<IAddressFormatter>() is { } formatter)
        {
            return [.. await formatter.GetLinesAsync(address)];
        }

        return [.. new[] { address.AddressLine1, address.AddressLine2, string.Join(" ", new[] { address.City, address.Region, address.PostalCode }.Where(part => !string.IsNullOrWhiteSpace(part))), address.Country }
            .Where(line => !string.IsNullOrWhiteSpace(line))];
    }
}
