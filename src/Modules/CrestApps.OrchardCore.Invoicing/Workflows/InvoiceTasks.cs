using System.Globalization;
using System.Reflection;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Invoicing.Workflows;

/// <summary>
/// A workflow task about invoices. Its fields are Liquid expressions. It writes the invoice and the command's errors to
/// the workflow output, under <see cref="OutputName"/> (<c>Invoice</c> when empty).
/// </summary>
/// <typeparam name="TActivity">The task type.</typeparam>
public abstract class InvoiceTaskActivityBase<TActivity> : TaskActivity<TActivity>
    where TActivity : ITask
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceTaskActivityBase{TActivity}"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The evaluator of the Liquid fields.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected InvoiceTaskActivityBase(IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Invoicing"];

    /// <summary>
    /// Gets or sets the name of the workflow output the task writes, read in Liquid as <c>Workflow.Output.Name</c>.
    /// </summary>
    public string OutputName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Writes the invoice (or only its id) and the errors to the workflow output.
    /// </summary>
    /// <param name="workflowContext">The workflow.</param>
    /// <param name="invoice">The invoice, if there is one.</param>
    /// <param name="invoiceId">The invoice's id when there is no invoice.</param>
    /// <param name="errors">Why the command failed.</param>
    /// <returns>The output, for a task to add to.</returns>
    protected Dictionary<string, object> WriteOutput(WorkflowExecutionContext workflowContext, Invoice invoice, string invoiceId, IEnumerable<string> errors)
    {
        var output = invoice is null
            ? new Dictionary<string, object>(StringComparer.Ordinal) { [InvoiceWorkflowInput.InvoiceId] = invoiceId }
            : InvoiceWorkflowInput.ToDictionary(invoice);

        output["Errors"] = (errors ?? []).ToArray();
        workflowContext.Output[string.IsNullOrWhiteSpace(OutputName) ? "Invoice" : OutputName.Trim()] = output;

        return output;
    }

    /// <summary>
    /// Evaluates a Liquid field.
    /// </summary>
    /// <param name="expression">The field.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The value, trimmed, or <see langword="null"/> when the field or its value is empty.</returns>
    protected async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Evaluates a Liquid field that holds an amount, written with a dot as the decimal separator.
    /// </summary>
    /// <param name="expression">The field.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The amount, or <see langword="null"/> when the field is empty or not a number.</returns>
    protected async Task<decimal?> EvaluateAmountAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        var value = await EvaluateAsync(expression, workflowContext);

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : null;
    }
}

/// <summary>
/// A workflow task that runs a command on one invoice. It ends with <c>Done</c>, with <c>NotFound</c> when no invoice has
/// the id, or with <c>Failed</c> when the command was refused (the reasons are in the output's <c>Errors</c>).
/// </summary>
/// <typeparam name="TActivity">The task type.</typeparam>
public abstract class InvoiceTaskActivity<TActivity> : InvoiceTaskActivityBase<TActivity>
    where TActivity : ITask
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceTaskActivity{TActivity}"/> class.
    /// </summary>
    /// <param name="invoiceManager">The invoice manager.</param>
    /// <param name="expressionEvaluator">The evaluator of the Liquid fields.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected InvoiceTaskActivity(IInvoiceManager invoiceManager, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        InvoiceManager = invoiceManager;
    }

    /// <summary>
    /// Gets the invoice manager.
    /// </summary>
    protected IInvoiceManager InvoiceManager { get; }

    /// <summary>
    /// Gets or sets the id of the invoice, a Liquid expression.
    /// </summary>
    public string InvoiceId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["NotFound"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var invoiceId = await EvaluateAsync(InvoiceId, workflowContext);
        var invoice = string.IsNullOrEmpty(invoiceId) ? null : await InvoiceManager.FindByIdAsync(invoiceId);

        if (invoice is null)
        {
            return Outcome("NotFound");
        }

        var (current, errors, extra) = await RunAsync(invoice, workflowContext);
        var output = WriteOutput(workflowContext, current ?? invoice, invoiceId, errors);

        foreach (var (key, value) in extra ?? new Dictionary<string, object>())
        {
            output[key] = value;
        }

        return Outcome(errors.Count == 0 ? "Done" : "Failed");
    }

    /// <summary>
    /// Runs the task's command on the invoice.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="workflowContext">The workflow, for evaluating the other fields.</param>
    /// <returns>The invoice after the command, why it failed, and any other values for the output.</returns>
    protected abstract Task<(Invoice Invoice, IList<string> Errors, IDictionary<string, object> Extra)> RunAsync(Invoice invoice, WorkflowExecutionContext workflowContext);
}

/// <summary>
/// Creates a draft invoice with one line for a bill-to party.
/// </summary>
public sealed class CreateInvoiceTask : InvoiceTaskActivityBase<CreateInvoiceTask>
{
    private readonly IInvoiceManager _invoiceManager;

    public CreateInvoiceTask(IInvoiceManager invoiceManager, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<CreateInvoiceTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _invoiceManager = invoiceManager;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Create Invoice"];

    /// <summary>
    /// Gets or sets the bill-to party, written <c>Kind:Id</c> (such as <c>Account:123</c>).
    /// </summary>
    public string BillTo
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the currency code; the bill-to's or the site's default when empty.
    /// </summary>
    public string Currency
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the line's name.
    /// </summary>
    public string Description
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the line's amount, with a dot as the decimal separator.
    /// </summary>
    public string Amount
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a reference kept on the invoice, such as an order number.
    /// </summary>
    public string Reference
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var errors = new List<string>();

        if (!PartyRef.TryParse(await EvaluateAsync(BillTo, workflowContext), out var party))
        {
            errors.Add(S["The bill-to must be written as Kind:Id, such as Account:123."].Value);
        }

        var description = await EvaluateAsync(Description, workflowContext);
        var amount = await EvaluateAmountAsync(Amount, workflowContext);

        if (string.IsNullOrEmpty(description))
        {
            errors.Add(S["The line needs a description."].Value);
        }

        if (amount is null)
        {
            errors.Add(S["The amount must be a number written with a dot as the decimal separator."].Value);
        }

        if (errors.Count > 0)
        {
            WriteOutput(workflowContext, invoice: null, invoiceId: null, errors);

            return Outcome("Failed");
        }

        var invoice = await _invoiceManager.NewAsync();

        invoice.BillTo ??= new DocumentParty();
        invoice.BillTo.Party = party;
        invoice.Currency = (await EvaluateAsync(Currency, workflowContext))?.ToUpperInvariant();
        invoice.Reference = await EvaluateAsync(Reference, workflowContext);
        invoice.Lines.Add(new DocumentLine
        {
            Id = global::OrchardCore.IdGenerator.GenerateId(),
            Name = description,
            Quantity = 1m,
            UnitPrice = amount.Value,
        });

        var validation = await _invoiceManager.ValidateAsync(invoice);

        if (!validation.Succeeded)
        {
            WriteOutput(workflowContext, invoice: null, invoiceId: null, validation.Errors.Select(error => error.ErrorMessage));

            return Outcome("Failed");
        }

        await _invoiceManager.CreateAsync(invoice);
        WriteOutput(workflowContext, invoice, invoice.ItemId, []);

        return Outcome("Done");
    }
}

/// <summary>
/// Issues a draft invoice: numbers it and posts it to the ledger.
/// </summary>
public sealed class IssueInvoiceTask : InvoiceTaskActivity<IssueInvoiceTask>
{
    private readonly IInvoiceCommandService _commands;

    public IssueInvoiceTask(IInvoiceManager invoiceManager, IInvoiceCommandService commands, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<IssueInvoiceTask> stringLocalizer)
        : base(invoiceManager, expressionEvaluator, stringLocalizer)
    {
        _commands = commands;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Issue Invoice"];

    /// <inheritdoc />
    protected override async Task<(Invoice Invoice, IList<string> Errors, IDictionary<string, object> Extra)> RunAsync(Invoice invoice, WorkflowExecutionContext workflowContext)
    {
        var result = await _commands.IssueAsync(invoice.ItemId);

        return (result.Invoice, result.Errors, null);
    }
}

/// <summary>
/// Sends an invoice to its customer by e-mail with its PDF.
/// </summary>
public sealed class SendInvoiceTask : InvoiceTaskActivity<SendInvoiceTask>
{
    private readonly IInvoiceSender _sender;

    public SendInvoiceTask(IInvoiceManager invoiceManager, IInvoiceSender sender, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<SendInvoiceTask> stringLocalizer)
        : base(invoiceManager, expressionEvaluator, stringLocalizer)
    {
        _sender = sender;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Send Invoice"];

    /// <summary>
    /// Gets or sets the address it is sent to; the bill-to's e-mail when empty.
    /// </summary>
    public string To
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override async Task<(Invoice Invoice, IList<string> Errors, IDictionary<string, object> Extra)> RunAsync(Invoice invoice, WorkflowExecutionContext workflowContext)
    {
        var result = await _sender.SendAsync(new InvoiceSendRequest
        {
            InvoiceId = invoice.ItemId,
            To = await EvaluateAsync(To, workflowContext),
        });

        return (result.Invoice, result.Errors, null);
    }
}

/// <summary>
/// Records a payment received outside the site toward an issued invoice, on its transaction.
/// </summary>
public sealed class RecordInvoicePaymentTask : InvoiceTaskActivity<RecordInvoicePaymentTask>
{
    private readonly ITransactionCommandService _transactionCommands;

    public RecordInvoicePaymentTask(IInvoiceManager invoiceManager, ITransactionCommandService transactionCommands, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<RecordInvoicePaymentTask> stringLocalizer)
        : base(invoiceManager, expressionEvaluator, stringLocalizer)
    {
        _transactionCommands = transactionCommands;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Record Invoice Payment"];

    /// <summary>
    /// Gets or sets the amount received, with a dot as the decimal separator; what is still due when empty.
    /// </summary>
    public string Amount
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets how the payment was made.
    /// </summary>
    public string PaymentMethod
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the payment's reference, such as a check number.
    /// </summary>
    public string Reference
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a note kept on the transaction.
    /// </summary>
    public string Note
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override async Task<(Invoice Invoice, IList<string> Errors, IDictionary<string, object> Extra)> RunAsync(Invoice invoice, WorkflowExecutionContext workflowContext)
    {
        if (invoice.Status != InvoiceStatus.Open || string.IsNullOrEmpty(invoice.TransactionId))
        {
            return (invoice, [S["Only an open invoice can receive a payment."].Value], null);
        }

        var amount = string.IsNullOrWhiteSpace(Amount) ? invoice.AmountDue : await EvaluateAmountAsync(Amount, workflowContext);

        if (amount is not > 0m)
        {
            return (invoice, [S["The amount must be a number above zero, written with a dot as the decimal separator."].Value], null);
        }

        var result = await _transactionCommands.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest
        {
            Amount = amount.Value,
            PaymentMethod = await EvaluateAsync(PaymentMethod, workflowContext),
            Reference = await EvaluateAsync(Reference, workflowContext),
            Note = await EvaluateAsync(Note, workflowContext),
        });

        // The invoice follows its transaction once the payment is saved; the output carries what was paid.
        return (await InvoiceManager.FindByIdAsync(invoice.ItemId) ?? invoice, result.Errors.ToList(), new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["PaymentAmount"] = amount.Value,
        });
    }
}

/// <summary>
/// Voids an open invoice nothing was paid toward.
/// </summary>
public sealed class VoidInvoiceTask : InvoiceTaskActivity<VoidInvoiceTask>
{
    private readonly IInvoiceCommandService _commands;

    public VoidInvoiceTask(IInvoiceManager invoiceManager, IInvoiceCommandService commands, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<VoidInvoiceTask> stringLocalizer)
        : base(invoiceManager, expressionEvaluator, stringLocalizer)
    {
        _commands = commands;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Void Invoice"];

    /// <summary>
    /// Gets or sets why the invoice is voided; required.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override async Task<(Invoice Invoice, IList<string> Errors, IDictionary<string, object> Extra)> RunAsync(Invoice invoice, WorkflowExecutionContext workflowContext)
    {
        var result = await _commands.VoidAsync(invoice.ItemId, await EvaluateAsync(Reason, workflowContext));

        return (result.Invoice, result.Errors, null);
    }
}

/// <summary>
/// Credits everything still creditable on an issued invoice with a credit note, and issues it.
/// </summary>
public sealed class CreateCreditNoteTask : InvoiceTaskActivity<CreateCreditNoteTask>
{
    private readonly ICreditNoteCommandService _creditNotes;
    private readonly ICreditNoteStore _creditNoteStore;

    public CreateCreditNoteTask(
        IInvoiceManager invoiceManager,
        ICreditNoteCommandService creditNotes,
        ICreditNoteStore creditNoteStore,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<CreateCreditNoteTask> stringLocalizer)
        : base(invoiceManager, expressionEvaluator, stringLocalizer)
    {
        _creditNotes = creditNotes;
        _creditNoteStore = creditNoteStore;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Create Credit Note"];

    /// <summary>
    /// Gets or sets why the invoice is credited.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets what happens to the credited amount: ReduceBalance, Refund or CustomerCredit. The default for the
    /// invoice when empty.
    /// </summary>
    public string Disposition
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override async Task<(Invoice Invoice, IList<string> Errors, IDictionary<string, object> Extra)> RunAsync(Invoice invoice, WorkflowExecutionContext workflowContext)
    {
        var dispositionText = await EvaluateAsync(Disposition, workflowContext);
        CreditNoteDisposition? disposition = null;

        if (dispositionText is not null)
        {
            if (!Enum.TryParse<CreditNoteDisposition>(dispositionText, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return (invoice, [S["The disposition must be ReduceBalance, Refund or CustomerCredit."].Value], null);
            }

            disposition = parsed;
        }

        var draft = await _creditNotes.CreateDraftAsync(invoice.ItemId);

        if (!draft.Succeeded)
        {
            return (draft.Invoice, draft.Errors, null);
        }

        var creditNote = draft.CreditNote;
        var reason = await EvaluateAsync(Reason, workflowContext);

        if (reason is not null || disposition.HasValue)
        {
            creditNote.Reason = reason ?? creditNote.Reason;
            creditNote.Disposition = disposition ?? creditNote.Disposition;

            await _creditNoteStore.UpdateAsync(creditNote);
        }

        var issued = await _creditNotes.IssueAsync(creditNote.ItemId);

        return (issued.Invoice, issued.Errors, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["CreditNoteId"] = creditNote.ItemId,
            ["CreditNoteNumber"] = issued.CreditNote?.Number,
            ["CreditNoteTotal"] = issued.CreditNote?.Totals?.Total ?? 0m,
        });
    }
}

/// <summary>
/// The editor of an invoice workflow task: copies the task's text fields to and from <see cref="InvoiceTaskViewModel"/>.
/// </summary>
/// <typeparam name="TActivity">The task type.</typeparam>
public sealed class InvoiceTaskDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, InvoiceTaskViewModel>
    where TActivity : InvoiceTaskActivityBase<TActivity>, ITask
{
    private static readonly PropertyInfo[] _modelProperties = typeof(InvoiceTaskViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceTaskDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InvoiceTaskDisplayDriver(IStringLocalizer<InvoiceTaskDisplayDriver<TActivity>> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
        => Initialize<InvoiceTaskViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));

    /// <inheritdoc />
    protected override void EditActivity(TActivity activity, InvoiceTaskViewModel model)
    {
        foreach (var property in _modelProperties)
        {
            var source = typeof(TActivity).GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance);

            if (source is not null && source.PropertyType == typeof(string))
            {
                property.SetValue(model, source.GetValue(activity));
            }
        }
    }

    /// <inheritdoc />
    protected override void UpdateActivity(InvoiceTaskViewModel model, TActivity activity)
    {
        foreach (var property in _modelProperties)
        {
            var target = typeof(TActivity).GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance);

            if (target is not null && target.PropertyType == typeof(string) && target.CanWrite)
            {
                var value = property.GetValue(model) as string;

                target.SetValue(activity, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
            }
        }
    }
}

/// <summary>
/// The editor of an invoice workflow task. Each task shows the fields it has; all of them are Liquid expressions.
/// </summary>
public class InvoiceTaskViewModel
{
    public string InvoiceId { get; set; }

    public string BillTo { get; set; }

    public string Currency { get; set; }

    public string Description { get; set; }

    public string Amount { get; set; }

    public string Reference { get; set; }

    public string To { get; set; }

    public string PaymentMethod { get; set; }

    public string Note { get; set; }

    public string Reason { get; set; }

    public string Disposition { get; set; }

    public string OutputName { get; set; }
}
