using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Numbering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using System.Security.Claims;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The default <see cref="IEstimateCommandService"/>. Each command runs under the estimate's lock, in one unit of work.
/// </summary>
public sealed class DefaultEstimateCommandService : IEstimateCommandService
{
    /// <summary>
    /// The prefix of the lock an estimate command takes.
    /// </summary>
    public const string LockPrefix = "Invoicing:Estimate:";

    private const int MaxNumberAttempts = 1000;
    private const int ExpiryBatch = 100;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly IEstimateStore _store;
    private readonly IEstimateManager _estimates;
    private readonly IInvoiceManager _invoices;
    private readonly IDocumentCalculator _calculator;
    private readonly INumberSeries _numberSeries;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DefaultEstimateCommandService(
        IEstimateStore store,
        IEstimateManager estimates,
        IInvoiceManager invoices,
        IDocumentCalculator calculator,
        INumberSeries numberSeries,
        IDistributedLock distributedLock,
        ISession session,
        ISiteService siteService,
        ILocalClock localClock,
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        ILogger<DefaultEstimateCommandService> logger,
        IStringLocalizer<DefaultEstimateCommandService> stringLocalizer)
    {
        _store = store;
        _estimates = estimates;
        _invoices = invoices;
        _calculator = calculator;
        _numberSeries = numberSeries;
        _distributedLock = distributedLock;
        _session = session;
        _siteService = siteService;
        _localClock = localClock;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<EstimateCommandResult> SendAsync(string estimateId, CancellationToken cancellationToken = default)
        => RunAsync(estimateId, async (estimate, result) =>
        {
            if (estimate.Status == EstimateStatus.Sent)
            {
                result.AlreadyDone = true;

                return null;
            }

            if (estimate.Status != EstimateStatus.Draft)
            {
                result.Fail(S["Only a draft estimate can be sent."]);

                return null;
            }

            var settings = await GetSettingsAsync();
            var today = await GetTodayAsync();

            estimate.Currency = string.IsNullOrWhiteSpace(estimate.Currency) ? settings.DefaultCurrency : estimate.Currency.Trim().ToUpperInvariant();
            estimate.Notes ??= settings.DefaultNotes;
            estimate.Terms ??= settings.DefaultTerms;
            estimate.Footer ??= settings.DefaultFooter;

            foreach (var error in InvoiceIssueRules.ValidateForIssue(estimate))
            {
                result.Fail(InvoiceIssueMessages.Describe(error, estimate, S));
            }

            if (!result.Succeeded)
            {
                return null;
            }

            await InvoiceCalculation.ApplyAsync(_calculator, estimate, settings, today, cancellationToken);

            foreach (var error in InvoiceIssueRules.ValidateTotals(estimate))
            {
                result.Fail(InvoiceIssueMessages.Describe(error, estimate, S));
            }

            if (!result.Succeeded)
            {
                return null;
            }

            var number = await NextNumberAsync(estimate, cancellationToken);

            if (number is null)
            {
                result.Fail(S["The estimate number series keeps giving numbers other estimates already hold. Change its format under Settings > Business documents > Number series."]);

                return null;
            }

            var now = _clock.UtcNow;

            estimate.Number = number;
            estimate.BaseNumber ??= number;
            estimate.Status = EstimateStatus.Sent;
            estimate.IssueDate ??= today;
            estimate.ExpiryDate ??= today.AddDays(Math.Clamp(settings.EstimateValidDays <= 0 ? 30 : settings.EstimateValidDays, 1, 3650));
            estimate.IssuedUtc = now;
            estimate.SentUtc = now;

            AddHistory(estimate, DocumentEventKinds.Sent, S["Sent as {0}, valid until {1:d}.", number, estimate.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue)].Value);

            return observer => observer.SentAsync(estimate, cancellationToken);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<EstimateCommandResult> AcceptAsync(string estimateId, EstimateAcceptanceRequest request, CancellationToken cancellationToken = default)
        => RunAsync(estimateId, async (estimate, result) =>
        {
            ArgumentNullException.ThrowIfNull(request);

            if (estimate.Status is EstimateStatus.Accepted or EstimateStatus.Invoiced && estimate.Acceptance is not null)
            {
                result.AlreadyDone = true;

                return null;
            }

            if (estimate.Status != EstimateStatus.Sent)
            {
                result.Fail(S["This estimate can no longer be accepted."]);

                return null;
            }

            if (estimate.ExpiryDate is { } expiry && expiry < request.Today)
            {
                result.Fail(S["This estimate expired on {0:d}. Ask for a new one.", expiry.ToDateTime(TimeOnly.MinValue)]);

                return null;
            }

            var name = request.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                result.Fail(S["Type your name to accept the estimate."]);

                return null;
            }

            var now = _clock.UtcNow;

            estimate.Acceptance = new EstimateAcceptance
            {
                Name = name,
                AcceptedUtc = now,
                IpAddress = request.IpAddress,
                TermsShown = estimate.Terms,
                UserId = request.UserId,
            };
            estimate.Status = EstimateStatus.Accepted;
            estimate.ClosedUtc = now;

            AddHistory(estimate, DocumentEventKinds.Accepted, S["Accepted by {0}.", name].Value, actorName: name);

            Invoice invoice = null;

            if (estimate.OnAcceptance != EstimateConversion.None)
            {
                invoice = await ConvertCoreAsync(estimate, estimate.OnAcceptance, result, cancellationToken);
            }

            result.Invoice = invoice;

            return async observer =>
            {
                await observer.AcceptedAsync(estimate, cancellationToken);

                if (invoice is not null)
                {
                    await observer.ConvertedAsync(estimate, invoice, cancellationToken);
                }
            };
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<EstimateCommandResult> DeclineAsync(string estimateId, string reason, CancellationToken cancellationToken = default)
        => RunAsync(estimateId, (estimate, result) =>
        {
            if (estimate.Status == EstimateStatus.Declined)
            {
                result.AlreadyDone = true;

                return Task.FromResult<Func<IEstimateObserver, Task>>(null);
            }

            if (estimate.Status != EstimateStatus.Sent)
            {
                result.Fail(S["This estimate can no longer be declined."]);

                return Task.FromResult<Func<IEstimateObserver, Task>>(null);
            }

            estimate.Status = EstimateStatus.Declined;
            estimate.DeclineReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            estimate.ClosedUtc = _clock.UtcNow;

            AddHistory(estimate, DocumentEventKinds.Declined, estimate.DeclineReason is null ? S["Declined by the customer."].Value : S["Declined by the customer: {0}", estimate.DeclineReason].Value);

            return Task.FromResult<Func<IEstimateObserver, Task>>(observer => observer.DeclinedAsync(estimate, cancellationToken));
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<EstimateCommandResult> CancelAsync(string estimateId, string reason, CancellationToken cancellationToken = default)
        => RunAsync(estimateId, (estimate, result) =>
        {
            if (estimate.Status == EstimateStatus.Canceled)
            {
                result.AlreadyDone = true;

                return Task.FromResult<Func<IEstimateObserver, Task>>(null);
            }

            if (estimate.Status == EstimateStatus.Invoiced)
            {
                result.Fail(S["An invoiced estimate can't be canceled. Void or credit its invoice instead."]);

                return Task.FromResult<Func<IEstimateObserver, Task>>(null);
            }

            estimate.Status = EstimateStatus.Canceled;
            estimate.ClosedUtc = _clock.UtcNow;

            AddHistory(estimate, DocumentEventKinds.Canceled, string.IsNullOrWhiteSpace(reason) ? S["Canceled."].Value : S["Canceled: {0}", reason.Trim()].Value);

            return Task.FromResult<Func<IEstimateObserver, Task>>(null);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<EstimateCommandResult> ReviseAsync(string estimateId, CancellationToken cancellationToken = default)
        => RunAsync(estimateId, async (estimate, result) =>
        {
            if (estimate.Status is not (EstimateStatus.Sent or EstimateStatus.Declined or EstimateStatus.Expired))
            {
                result.Fail(S["Only a sent, declined or expired estimate can be revised."]);

                return null;
            }

            var revision = await _estimates.NewAsync();

            revision.Revision = estimate.Revision + 1;
            revision.BaseNumber = estimate.BaseNumber ?? estimate.Number;
            revision.RevisionOfId = estimate.ItemId;
            revision.BillTo = CopyParty(estimate.BillTo);
            revision.Currency = estimate.Currency;
            revision.PricesIncludeTax = estimate.PricesIncludeTax;
            revision.Lines = [.. estimate.Lines.Select(line => CopyLine(line, keepId: true))];
            revision.Charges = [.. estimate.Charges];
            revision.Notes = estimate.Notes;
            revision.Terms = estimate.Terms;
            revision.Footer = estimate.Footer;
            revision.InternalNote = estimate.InternalNote;
            revision.PurchaseOrderNumber = estimate.PurchaseOrderNumber;
            revision.Reference = estimate.Reference;
            revision.TemplateId = estimate.TemplateId;
            revision.DepositPercent = estimate.DepositPercent;
            revision.DepositAmount = estimate.DepositAmount;
            revision.OnAcceptance = estimate.OnAcceptance;

            AddHistory(revision, DocumentEventKinds.Created, S["Revision {0} of {1}.", revision.Revision, revision.BaseNumber].Value);

            await _estimates.CreateAsync(revision);

            estimate.Status = EstimateStatus.Canceled;
            estimate.ReplacedById = revision.ItemId;
            estimate.ClosedUtc = _clock.UtcNow;

            AddHistory(estimate, DocumentEventKinds.Canceled, S["Replaced by revision {0}.", revision.Revision].Value);

            result.Revision = revision;

            return null;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<EstimateCommandResult> ConvertAsync(string estimateId, EstimateConversion conversion, CancellationToken cancellationToken = default)
        => RunAsync(estimateId, async (estimate, result) =>
        {
            if (conversion == EstimateConversion.None)
            {
                result.Fail(S["Choose what to invoice: the whole estimate or its deposit."]);

                return null;
            }

            if (estimate.Status is not (EstimateStatus.Sent or EstimateStatus.Accepted or EstimateStatus.Invoiced))
            {
                result.Fail(S["Only a sent or accepted estimate can be invoiced."]);

                return null;
            }

            var invoice = await ConvertCoreAsync(estimate, conversion, result, cancellationToken);

            if (invoice is null)
            {
                return null;
            }

            result.Invoice = invoice;

            return observer => observer.ConvertedAsync(estimate, invoice, cancellationToken);
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> ExpireDueAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var expired = 0;

        foreach (var candidate in await _store.ListExpiringAsync(today, ExpiryBatch, cancellationToken))
        {
            var result = await RunAsync(candidate.ItemId, (estimate, result) =>
            {
                if (estimate.Status != EstimateStatus.Sent || estimate.ExpiryDate is not { } expiry || expiry >= today)
                {
                    result.AlreadyDone = true;

                    return Task.FromResult<Func<IEstimateObserver, Task>>(null);
                }

                estimate.Status = EstimateStatus.Expired;
                estimate.ClosedUtc = _clock.UtcNow;

                AddHistory(estimate, DocumentEventKinds.Expired, S["Expired unanswered after {0:d}.", expiry.ToDateTime(TimeOnly.MinValue)].Value);

                return Task.FromResult<Func<IEstimateObserver, Task>>(observer => observer.ExpiredAsync(estimate, cancellationToken));
            }, cancellationToken);

            if (result.Succeeded && !result.AlreadyDone)
            {
                expired++;
            }
        }

        return expired;
    }

    // Makes the draft invoice of an estimate, or returns the one made already.
    private async Task<Invoice> ConvertCoreAsync(Estimate estimate, EstimateConversion conversion, EstimateCommandResult result, CancellationToken cancellationToken)
    {
        var kind = conversion == EstimateConversion.Deposit ? InvoiceKind.Prepayment : InvoiceKind.Standard;

        foreach (var invoiceId in estimate.InvoiceIds)
        {
            if (await _invoices.FindByIdAsync(invoiceId, cancellationToken) is { } made && made.Kind == kind && made.Status != InvoiceStatus.Void)
            {
                result.AlreadyDone = true;

                return made;
            }
        }

        var settings = await GetSettingsAsync();
        var invoice = await _invoices.NewAsync(cancellationToken: cancellationToken);

        invoice.Kind = kind;
        invoice.BillTo = CopyParty(estimate.BillTo);
        invoice.Currency = estimate.Currency;
        invoice.PricesIncludeTax = estimate.PricesIncludeTax;
        invoice.PurchaseOrderNumber = estimate.PurchaseOrderNumber;
        invoice.Reference = estimate.Number;
        invoice.SourceType = InvoicingConstants.EstimateReferenceType;
        invoice.SourceId = estimate.ItemId;
        invoice.PaymentTermId = settings.DefaultPaymentTermId;
        invoice.AllowPartialPayment = settings.AllowPartialPayment;

        if (kind == InvoiceKind.Prepayment)
        {
            var total = estimate.Totals?.Total ?? 0m;
            var amount = estimate.DepositAmount is > 0m ? estimate.DepositAmount.Value
                : estimate.DepositPercent is > 0m ? Math.Round(total * estimate.DepositPercent.Value / 100m, 2, MidpointRounding.AwayFromZero)
                : 0m;

            if (amount <= 0m)
            {
                result.Fail(S["This estimate asks for no deposit."]);

                return null;
            }

            invoice.Lines.Add(new DocumentLine
            {
                Id = IdGenerator.GenerateId(),
                Kind = DocumentLineKind.Deposit,
                Name = S["Deposit on estimate {0}", estimate.Number].Value,
                Quantity = 1m,
                UnitPrice = amount,
                SourceType = InvoicingConstants.EstimateReferenceType,
                SourceId = estimate.ItemId,
            });
        }
        else
        {
            invoice.Lines = [.. estimate.Lines.Select(line => CopyLine(line, keepId: false))];
            invoice.Charges = [.. estimate.Charges];
            invoice.Notes = estimate.Notes;
            invoice.Terms = estimate.Terms;
            invoice.Footer = estimate.Footer;
        }

        await InvoiceCalculation.ApplyAsync(_calculator, invoice, settings, issueDate: null, cancellationToken);

        var validation = await _invoices.ValidateAsync(invoice, cancellationToken);

        if (!validation.Succeeded)
        {
            foreach (var error in validation.Errors)
            {
                result.Fail(error.ErrorMessage);
            }

            return null;
        }

        await _invoices.CreateAsync(invoice, cancellationToken);

        estimate.InvoiceIds.Add(invoice.ItemId);

        if (kind == InvoiceKind.Standard)
        {
            estimate.Status = EstimateStatus.Invoiced;
        }

        AddHistory(estimate, DocumentEventKinds.Converted, kind == InvoiceKind.Prepayment
            ? S["Its deposit was put on a draft prepayment invoice."].Value
            : S["Put on a draft invoice."].Value);

        return invoice;
    }

    private async Task<string> NextNumberAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        if (estimate.Revision > 1 && !string.IsNullOrEmpty(estimate.BaseNumber))
        {
            var revised = $"{estimate.BaseNumber}-{estimate.Revision}";

            return await _store.FindByNumberAsync(revised, cancellationToken) is null ? revised : null;
        }

        for (var attempt = 0; attempt < MaxNumberAttempts; attempt++)
        {
            var number = await _numberSeries.NextAsync(InvoicingConstants.EstimateNumberSeries, null, cancellationToken);

            if (await _store.FindByNumberAsync(number.Text, cancellationToken) is null)
            {
                return number.Text;
            }
        }

        return null;
    }

    // Runs one command under the estimate's lock, saves what it changed with the rest of the unit of work, and tells the
    // observers after the save.
    private async Task<EstimateCommandResult> RunAsync(
        string estimateId,
        Func<Estimate, EstimateCommandResult, Task<Func<IEstimateObserver, Task>>> command,
        CancellationToken cancellationToken)
    {
        var result = new EstimateCommandResult();

        if (string.IsNullOrEmpty(estimateId))
        {
            return result.Fail(S["The estimate could not be found."]);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + estimateId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return result.Fail(S["This estimate is being changed by someone else. Try again in a moment."]);
        }

        await using var _ = locker;

        var estimate = await _store.FindByIdAsync(estimateId, cancellationToken);

        if (estimate is null)
        {
            return result.Fail(S["The estimate could not be found."]);
        }

        result.Estimate = estimate;

        Func<IEstimateObserver, Task> notify;

        try
        {
            notify = await command(estimate, result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _session.CancelAsync();

            throw;
        }

        if (result.AlreadyDone && result.Invoice is null && result.Revision is null)
        {
            return result;
        }

        if (!result.Succeeded)
        {
            await _session.CancelAsync();

            return result;
        }

        estimate.ModifiedUtc = _clock.UtcNow;

        try
        {
            await _store.UpdateAsync(estimate, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await _session.CancelAsync();

            return result.Fail(S["This estimate was changed by someone else while you were working on it. Reload it and try again."]);
        }

        if (result.Revision is not null)
        {
            result.Estimate = result.Revision;
        }

        if (notify is not null)
        {
            foreach (var observer in _serviceProvider.GetServices<IEstimateObserver>())
            {
                try
                {
                    await notify(observer);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "An estimate observer '{Observer}' failed on estimate '{EstimateId}'.", observer.GetType().FullName, estimate.ItemId);
                }
            }
        }

        return result;
    }

    private void AddHistory(Estimate estimate, string kind, string message, string actorName = null)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        estimate.History.Add(new DocumentEvent
        {
            Id = IdGenerator.GenerateId(),
            Kind = kind,
            CreatedUtc = _clock.UtcNow,
            ActorId = actorName is null ? user?.FindFirstValue(ClaimTypes.NameIdentifier) : null,
            ActorName = actorName ?? user?.Identity?.Name,
            Message = message,
        });
    }

    private static DocumentParty CopyParty(DocumentParty party)
        => party is null ? new DocumentParty() : new DocumentParty
        {
            Party = party.Party,
            Contact = party.Contact,
            ShippingAddress = party.ShippingAddress,
            TaxId = party.TaxId,
            TaxTreatment = party.TaxTreatment,
            ExemptionReason = party.ExemptionReason,
            CopyTo = [.. party.CopyTo ?? []],
        };

    private static DocumentLine CopyLine(DocumentLine line, bool keepId)
        => new()
        {
            Id = keepId ? line.Id : IdGenerator.GenerateId(),
            Kind = line.Kind,
            ProductId = line.ProductId,
            Sku = line.Sku,
            Name = line.Name,
            Description = line.Description,
            Quantity = line.Quantity,
            Unit = line.Unit,
            UnitPrice = line.UnitPrice,
            DiscountPercent = line.DiscountPercent,
            DiscountAmount = line.DiscountAmount,
            TaxCategoryCode = line.TaxCategoryCode,
            Taxes = [.. line.Taxes ?? []],
            TaxTreatment = line.TaxTreatment,
            TaxTreatmentReason = line.TaxTreatmentReason,
            ServiceStart = line.ServiceStart,
            ServiceEnd = line.ServiceEnd,
            SourceType = keepId ? line.SourceType : InvoicingConstants.EstimateReferenceType,
            SourceId = keepId ? line.SourceId : line.Id,
        };

    private async Task<DateOnly> GetTodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

    private async Task<InvoicingSettings> GetSettingsAsync()
        => await _siteService.GetSettingsAsync<InvoicingSettings>();
}
