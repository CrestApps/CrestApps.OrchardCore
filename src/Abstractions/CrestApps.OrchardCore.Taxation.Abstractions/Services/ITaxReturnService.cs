using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Gives the figures of a return from where a module keeps them: its ledger, its invoices, its orders or its payroll.
/// Tax Returns never reads sales itself, so a site with its own books files from them. Every provider runs for every
/// return; one that has nothing for the return's registration adds nothing. Register one with
/// <c>services.AddTaxReturnDataProvider&lt;T&gt;()</c>.
/// </summary>
public interface ITaxReturnDataProvider
{
    /// <summary>
    /// Gets the stored name, written on the lines it adds.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Adds the figures of the context's period to its lines, such as the taxable sales by category, the tax collected
    /// and the purchases owing use tax.
    /// </summary>
    /// <param name="context">The return, its registration and its period, and the lines to add to.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ContributeAsync(TaxReturnDataContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a data provider gives the figures of.
/// </summary>
public sealed class TaxReturnDataContext
{
    /// <summary>
    /// Gets the registration the return is filed under.
    /// </summary>
    public required MerchantTaxRegistration Registration { get; init; }

    /// <summary>
    /// Gets the return being prepared. Its lines are the ones to add to.
    /// </summary>
    public required TaxReturn Return { get; init; }

    /// <summary>
    /// Gets the period.
    /// </summary>
    public required TaxReturnPeriod Period { get; init; }

    /// <summary>
    /// Gets the lines; add the provider's figures here.
    /// </summary>
    public IList<TaxReturnLine> Lines
        => Return.Lines;

    /// <summary>
    /// Gets the notes of the return; add what a reviewer should know, such as a figure that was not available.
    /// </summary>
    public IList<string> Notes
        => Return.Notes;
}

/// <summary>
/// A return form: the boxes of a jurisdiction's return and how they are worked out from the lines, such as a state's
/// monthly sales and use tax return. The built-in generic form sums the lines by kind; a pack adds a jurisdiction's own
/// form with its discounts, penalties and box numbers. Register one with <c>services.AddTaxReturnForm&lt;T&gt;()</c>.
/// </summary>
public interface ITaxReturnForm
{
    /// <summary>
    /// Gets the stored name, such as <c>US-MI-5080</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets what people see.
    /// </summary>
    [JsonIgnore]
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Works out the return's boxes and its amount due from its lines.
    /// </summary>
    /// <param name="context">The return, its registration and its period.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ComputeAsync(TaxReturnFormContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a form works its boxes out from.
/// </summary>
public sealed class TaxReturnFormContext
{
    /// <summary>
    /// Gets the registration the return is filed under.
    /// </summary>
    public required MerchantTaxRegistration Registration { get; init; }

    /// <summary>
    /// Gets the return to fill: its boxes are cleared first, its lines are set.
    /// </summary>
    public required TaxReturn Return { get; init; }

    /// <summary>
    /// Gets the day the return is worked out as filed on: the day it was filed, or today for a draft past its due date,
    /// so a late return shows its penalty; <see langword="null"/> for a draft that is not late.
    /// </summary>
    public DateOnly? FiledOn { get; init; }

    /// <summary>
    /// Gets the services, for what a form needs more.
    /// </summary>
    public required IServiceProvider Services { get; init; }
}

/// <summary>
/// Hears what happens to returns: a module posts a filed return or its payment to its books, or records a payroll tax
/// deposit for the withholding paid with it. An observer runs after the change is saved in the session, before it is
/// committed; one that throws stops the change. Register one with <c>services.AddTaxReturnObserver&lt;T&gt;()</c>.
/// </summary>
public interface ITaxReturnObserver
{
    /// <summary>
    /// Called once a return changed.
    /// </summary>
    /// <param name="change">What changed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ChangedAsync(TaxReturnChange change, CancellationToken cancellationToken = default);
}

/// <summary>
/// A change of a return.
/// </summary>
public sealed class TaxReturnChange
{
    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public TaxReturnChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the return as it is after the change.
    /// </summary>
    public TaxReturn Return { get; set; }

    /// <summary>
    /// Gets or sets the payment recorded or removed, for <see cref="TaxReturnChangeKind.PaymentRecorded"/> and
    /// <see cref="TaxReturnChangeKind.PaymentRemoved"/>.
    /// </summary>
    public TaxReturnPayment Payment { get; set; }

    /// <summary>
    /// Gets or sets the user name of who made the change, or <see langword="null"/> for the system.
    /// </summary>
    public string Actor { get; set; }
}

/// <summary>
/// What happened to a return.
/// </summary>
public enum TaxReturnChangeKind
{
    /// <summary>
    /// A draft was prepared, or prepared again.
    /// </summary>
    Prepared,

    /// <summary>
    /// A draft was marked ready to file.
    /// </summary>
    Ready,

    /// <summary>
    /// A ready return was sent back to draft.
    /// </summary>
    Reopened,

    /// <summary>
    /// The return was marked as filed.
    /// </summary>
    Filed,

    /// <summary>
    /// An amendment of a filed return was started; the return is the new draft.
    /// </summary>
    Amended,

    /// <summary>
    /// A payment was recorded against the return.
    /// </summary>
    PaymentRecorded,

    /// <summary>
    /// A payment recorded by mistake was removed.
    /// </summary>
    PaymentRemoved,
}

/// <summary>
/// Stores tax returns.
/// </summary>
public interface ITaxReturnManager : ICatalogManager<TaxReturn>
{
    /// <summary>
    /// Lists the returns whose period ends in a span.
    /// </summary>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The returns.</returns>
    Task<IReadOnlyList<TaxReturn>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a registration has any return.
    /// </summary>
    /// <param name="registrationId">The registration.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    Task<bool> HasReturnsAsync(string registrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every version of a registration's return of a form for a period: the original and its amendments.
    /// </summary>
    /// <param name="registrationId">The registration.</param>
    /// <param name="form">The form.</param>
    /// <param name="periodKey">The period key.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The returns, oldest first.</returns>
    Task<IReadOnlyList<TaxReturn>> ListVersionsAsync(string registrationId, string form, string periodKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the current return of a registration's form for a period (the one not amended), or <see langword="null"/>.
    /// </summary>
    /// <param name="registrationId">The registration.</param>
    /// <param name="form">The form.</param>
    /// <param name="periodKey">The period key.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The return.</returns>
    Task<TaxReturn> FindCurrentAsync(string registrationId, string form, string periodKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Works out the periods each registration files for, and prepares, files, pays and amends their returns.
/// </summary>
public interface ITaxReturnService
{
    /// <summary>
    /// Gets the forms registered, by name.
    /// </summary>
    /// <returns>The forms.</returns>
    IReadOnlyDictionary<string, ITaxReturnForm> GetForms();

    /// <summary>
    /// Lists the periods the active registrations with a filing schedule file for that end in a span, with each one's
    /// due date and current return.
    /// </summary>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The periods, by due date.</returns>
    Task<IReadOnlyList<TaxReturnPeriodSummary>> ListPeriodsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares a registration's return of a period from the data providers: a new draft, or the draft prepared again.
    /// A ready or filed return is not prepared again.
    /// </summary>
    /// <param name="registrationId">The registration.</param>
    /// <param name="periodKey">The period key, such as <c>2026-10</c>.</param>
    /// <param name="annualReconciliation">Whether to prepare the yearly reconciliation return of the year.</param>
    /// <param name="user">Who prepares it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The return, or the errors.</returns>
    Task<TaxReturnResult> PrepareAsync(string registrationId, string periodKey, bool annualReconciliation, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares a draft again from the data providers.
    /// </summary>
    /// <param name="taxReturn">The draft.</param>
    /// <param name="user">Who prepares it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The return, or the errors.</returns>
    Task<TaxReturnResult> PrepareAgainAsync(TaxReturn taxReturn, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a draft ready to file: its figures are kept until it is filed or reopened.
    /// </summary>
    /// <param name="taxReturn">The draft.</param>
    /// <param name="user">Who checked it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TaxReturnResult> MarkReadyAsync(TaxReturn taxReturn, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a ready return back to draft.
    /// </summary>
    /// <param name="taxReturn">The ready return.</param>
    /// <param name="user">Who reopens it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TaxReturnResult> ReopenAsync(TaxReturn taxReturn, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a draft or ready return as filed: it never changes again, and the return it corrects is marked amended.
    /// A late draft is worked out again as filed on that day first, so its penalty is kept.
    /// </summary>
    /// <param name="taxReturn">The return.</param>
    /// <param name="filedOn">The day it was filed.</param>
    /// <param name="confirmationNumber">The agency's confirmation number, or <see langword="null"/>.</param>
    /// <param name="channel">The filing channel it went through, or <see langword="null"/> when filed by hand.</param>
    /// <param name="user">Who filed it, or <see langword="null"/> for the system.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TaxReturnResult> MarkFiledAsync(TaxReturn taxReturn, DateOnly filedOn, string confirmationNumber, string channel, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the correction of a filed return: a new draft, prepared again from the data providers, that names it and
    /// counts what was paid with it.
    /// </summary>
    /// <param name="taxReturn">The filed return.</param>
    /// <param name="user">Who amends it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The new draft, or the errors.</returns>
    Task<TaxReturnResult> AmendAsync(TaxReturn taxReturn, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a payment against a return. Tax Returns never moves money; observers post it to the books.
    /// </summary>
    /// <param name="taxReturn">The return.</param>
    /// <param name="payment">The payment: its amount, day, reference and method.</param>
    /// <param name="user">Who recorded it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TaxReturnResult> RecordPaymentAsync(TaxReturn taxReturn, TaxReturnPayment payment, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a payment recorded by mistake.
    /// </summary>
    /// <param name="taxReturn">The return.</param>
    /// <param name="paymentId">The payment's id.</param>
    /// <param name="user">Who removed it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TaxReturnResult> RemovePaymentAsync(TaxReturn taxReturn, string paymentId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of a return action.
/// </summary>
public sealed class TaxReturnResult
{
    /// <summary>
    /// Gets or sets the return.
    /// </summary>
    public TaxReturn Return { get; set; }

    /// <summary>
    /// Gets the errors.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets a value indicating whether it succeeded.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;
}
