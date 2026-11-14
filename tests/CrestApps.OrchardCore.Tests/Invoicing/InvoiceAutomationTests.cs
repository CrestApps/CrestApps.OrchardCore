using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Core.Handlers;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.Workflows;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// What happens to an invoice reaches the invoice observers once it is saved, so workflows and webhooks can react: a draft
/// created, issued, sent, opened, paid in part and in full, overdue, voided and credited. The workflow tasks run the same
/// commands staff run.
/// </summary>
public sealed class InvoiceAutomationTests
{
    [Fact]
    public async Task AnInvoicesLife_ReachesTheObservers_StepByStep()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();

        // Act
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
        var number = issued.Invoice.Number;
        await harness.InScopeAsync(services => services.GetRequiredService<IInvoiceSender>().SendAsync(new InvoiceSendRequest { InvoiceId = draft.ItemId }, TestContext.Current.CancellationToken));
        await harness.InScopeAsync(async services => await services.GetRequiredService<InvoiceViewTracker>().RecordAsync(await services.GetRequiredService<IInvoiceStore>().FindByIdAsync(draft.ItemId, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));
        await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(issued.Invoice.TransactionId, new TransactionPaymentRequest { Amount = 40m }, TestContext.Current.CancellationToken));
        await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(issued.Invoice.TransactionId, new TransactionPaymentRequest { Amount = 60m }, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(
            [
                $"Created:{draft.ItemId}",
                $"Issued:{number}",
                $"Sent:{number}",
                $"Viewed:{number}",
                $"PaymentReceived:{number}:40.00",
                $"PaymentReceived:{number}:60.00",
                $"Paid:{number}",
            ],
            harness.Observed);
    }

    [Fact]
    public async Task AVoid_AndACreditNote_ReachTheObservers()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var toVoid = await IssueAsync(harness);
        var toCredit = await IssueAsync(harness);
        harness.Observed.Clear();

        // Act
        await harness.RunAsync(commands => commands.VoidAsync(toVoid.ItemId, "Duplicate", TestContext.Current.CancellationToken));
        var draft = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(toCredit.ItemId, TestContext.Current.CancellationToken));
        var credited = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(draft.CreditNote.ItemId, TestContext.Current.CancellationToken));

        // Assert: a credit settles what was owed but is not a payment.
        Assert.True(credited.Succeeded, string.Join(" ", credited.Errors));
        Assert.Equal([$"Voided:{toVoid.Number}", $"CreditNoteIssued:{toCredit.Number}:{credited.CreditNote.Number}"], harness.Observed);
    }

    [Fact]
    public async Task ADebtTheLedgerFindsOverdue_MakesItsOpenInvoiceOverdue()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var open = await IssueAsync(harness);
        var voided = await IssueAsync(harness);
        await harness.RunAsync(commands => commands.VoidAsync(voided.ItemId, "Duplicate", TestContext.Current.CancellationToken));
        harness.Observed.Clear();

        // Act
        await harness.InScopeAsync(async services =>
        {
            var handler = services.GetServices<ITransactionEventHandler>().OfType<InvoiceTransactionHandler>().Single();
            var transactions = services.GetRequiredService<ITransactionManager>();

            await handler.BecameOverdueAsync(await transactions.FindByIdAsync(open.TransactionId, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
            await handler.BecameOverdueAsync(await transactions.FindByIdAsync(voided.TransactionId, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        Assert.Equal([$"Overdue:{open.Number}"], harness.Observed);
    }

    [Fact]
    public async Task TheWorkflowObserver_StartsTheEvent_WithTheInvoiceAsInput()
    {
        // Arrange
        var triggered = new List<(string Name, IDictionary<string, object> Input, string CorrelationId)>();
        var manager = new Mock<IWorkflowManager>();
        manager
            .Setup(candidate => candidate.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback((string name, IDictionary<string, object> input, string correlationId, bool _, bool _) => triggered.Add((name, input, correlationId)))
            .ReturnsAsync([]);

        var observer = new InvoiceWorkflowObserver(manager.Object);
        var invoice = CreateInvoice();
        var creditNote = new CreditNote { ItemId = "cn-1", Number = "CN-7", Disposition = CreditNoteDisposition.Refund, Totals = new DocumentTotals { Total = 30m } };

        // Act
        await observer.PaymentReceivedAsync(invoice, 40m, TestContext.Current.CancellationToken);
        await observer.CreditNoteIssuedAsync(creditNote, invoice, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([nameof(InvoicePaymentReceivedEvent), nameof(CreditNoteIssuedEvent)], triggered.Select(entry => entry.Name));
        Assert.All(triggered, entry => Assert.Equal("inv-1", entry.CorrelationId));
        Assert.Equal((40m, "INV-1001", 60m, "Account", "acct-1"), (triggered[0].Input["Amount"], triggered[0].Input["Number"], triggered[0].Input["AmountDue"], triggered[0].Input["BillToKind"], triggered[0].Input["BillToId"]));
        Assert.Equal(("CN-7", 30m, "Refund"), (triggered[1].Input["CreditNoteNumber"], triggered[1].Input["CreditNoteTotal"], triggered[1].Input["Disposition"]));
    }

    [Fact]
    public async Task TheWebhookObserver_PublishesEachEvent_WithAStableId()
    {
        // Arrange
        var published = new List<(string Name, string EventId)>();
        var publisher = new Mock<IWebhookPublisher>();
        publisher
            .Setup(candidate => candidate.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string name, object _, string eventId, CancellationToken _) => published.Add((name, eventId)))
            .Returns(Task.CompletedTask);

        var observer = new InvoiceWebhookObserver(publisher.Object);
        var invoice = CreateInvoice();

        // Act
        await observer.IssuedAsync(invoice, TestContext.Current.CancellationToken);
        await observer.VoidedAsync(invoice, TestContext.Current.CancellationToken);
        await observer.CreditNoteIssuedAsync(new CreditNote { ItemId = "cn-1" }, invoice, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                (InvoicingConstants.WebhookEvents.InvoiceIssued, "evt_invoice-issued_inv-1"),
                (InvoicingConstants.WebhookEvents.InvoiceVoided, "evt_invoice-voided_inv-1"),
                (InvoicingConstants.WebhookEvents.CreditNoteIssued, "evt_credit-note-issued_cn-1"),
            ],
            published);
    }

    [Fact]
    public async Task TheIssueAndPaymentTasks_IssueAnInvoice_AndPayItInFull()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();
        var context = CreateContext();

        // Act
        var issueOutcome = await harness.InScopeAsync(async services => (await new IssueInvoiceTask(
            services.GetRequiredService<IInvoiceManager>(),
            services.GetRequiredService<IInvoiceCommandService>(),
            Evaluator(),
            new PassThroughStringLocalizer<IssueInvoiceTask>())
        {
            InvoiceId = draft.ItemId,
        }.ExecuteAsync(context, null)).Outcomes.Single());

        var payOutcome = await harness.InScopeAsync(async services => (await new RecordInvoicePaymentTask(
            services.GetRequiredService<IInvoiceManager>(),
            services.GetRequiredService<ITransactionCommandService>(),
            Evaluator(),
            new PassThroughStringLocalizer<RecordInvoicePaymentTask>())
        {
            InvoiceId = draft.ItemId,
            Reference = "CHK-9",
            OutputName = "Paid",
        }.ExecuteAsync(context, null)).Outcomes.Single());

        // Assert
        Assert.Equal(("Done", "Done"), (issueOutcome, payOutcome));
        var paid = Assert.IsType<Dictionary<string, object>>(context.Output["Paid"]);
        Assert.Equal((100m, 0m, "Paid"), (paid["AmountPaid"], paid["AmountDue"], paid["Settlement"]));
        Assert.Equal(InvoiceSettlement.Paid, (await harness.FindInvoiceAsync(draft.ItemId)).Settlement);
    }

    [Fact]
    public async Task TheCreateTask_DraftsAnInvoice_AndRefusesABillToItCannotRead()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var context = CreateContext();

        // Act
        var outcomes = await harness.InScopeAsync(async services =>
        {
            CreateInvoiceTask Task(string billTo)
                => new(services.GetRequiredService<IInvoiceManager>(), Evaluator(), new PassThroughStringLocalizer<CreateInvoiceTask>())
                {
                    BillTo = billTo,
                    Description = "Monthly retainer",
                    Amount = "250.50",
                    Currency = "usd",
                };

            var refused = (await Task("acct-1").ExecuteAsync(context, null)).Outcomes.Single();
            var created = (await Task("Account:acct-1").ExecuteAsync(context, null)).Outcomes.Single();

            return (refused, created);
        });

        // Assert
        Assert.Equal(("Failed", "Done"), outcomes);
        var output = Assert.IsType<Dictionary<string, object>>(context.Output["Invoice"]);
        var stored = await harness.FindInvoiceAsync((string)output["InvoiceId"]);
        Assert.Equal((InvoiceStatus.Draft, "Account:acct-1", "USD"), (stored.Status, stored.BillTo.Party.ToString(), stored.Currency));
        var line = Assert.Single(stored.Lines);
        Assert.Equal(("Monthly retainer", 250.50m), (line.Name, line.UnitPrice));
    }

    [Fact]
    public async Task TheCreditNoteTask_CreditsTheInvoice_AndTheVoidTaskFindsNoUnknownInvoice()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        var context = CreateContext();

        // Act
        var outcomes = await harness.InScopeAsync(async services =>
        {
            var credit = (await new CreateCreditNoteTask(
                services.GetRequiredService<IInvoiceManager>(),
                services.GetRequiredService<ICreditNoteCommandService>(),
                services.GetRequiredService<ICreditNoteStore>(),
                Evaluator(),
                new PassThroughStringLocalizer<CreateCreditNoteTask>())
            {
                InvoiceId = invoice.ItemId,
                Reason = "Goodwill",
                Disposition = "ReduceBalance",
            }.ExecuteAsync(context, null)).Outcomes.Single();

            var missing = (await new VoidInvoiceTask(
                services.GetRequiredService<IInvoiceManager>(),
                services.GetRequiredService<IInvoiceCommandService>(),
                Evaluator(),
                new PassThroughStringLocalizer<VoidInvoiceTask>())
            {
                InvoiceId = "nope",
                Reason = "x",
            }.ExecuteAsync(context, null)).Outcomes.Single();

            return (credit, missing);
        });

        // Assert
        Assert.Equal(("Done", "NotFound"), outcomes);
        var output = Assert.IsType<Dictionary<string, object>>(context.Output["Invoice"]);
        Assert.Equal((100m, 0m), (output["AmountCredited"], output["AmountDue"]));
        Assert.False(string.IsNullOrEmpty((string)output["CreditNoteNumber"]));
        Assert.Contains(harness.Observed, entry => entry.StartsWith($"CreditNoteIssued:{invoice.Number}:", StringComparison.Ordinal));
    }

    private static Invoice CreateInvoice()
        => new()
        {
            ItemId = "inv-1",
            Number = "INV-1001",
            Status = InvoiceStatus.Open,
            Currency = "USD",
            Totals = new DocumentTotals { Total = 100m },
            AmountPaid = 40m,
            AmountDue = 60m,
            BillTo = new DocumentParty { Party = new CrestApps.OrchardCore.Parties.Models.PartyRef("Account", "acct-1") },
            CreatedUtc = InvoicingTestHarness.Now,
        };

    private static WorkflowExecutionContext CreateContext()
        => new(
            new WorkflowType { WorkflowTypeId = "t1" },
            new Workflow { WorkflowId = "w1" },
            new Dictionary<string, object>(StringComparer.Ordinal),
            new Dictionary<string, object>(StringComparer.Ordinal),
            new Dictionary<string, object>(StringComparer.Ordinal),
            [],
            null,
            []);

    // The fields are taken as they are written, as Liquid renders text without tags.
    private static IWorkflowExpressionEvaluator Evaluator()
    {
        var evaluator = new Mock<IWorkflowExpressionEvaluator>();
        evaluator
            .Setup(candidate => candidate.EvaluateAsync(It.IsAny<WorkflowExpression<string>>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<TextEncoder>()))
            .Returns((WorkflowExpression<string> expression, WorkflowExecutionContext _, TextEncoder _) => Task.FromResult(expression.Expression));

        return evaluator.Object;
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness)
    {
        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }
}
