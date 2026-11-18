using System.Security.Claims;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Invoicing.Api;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// The invoicing API drafts and issues an invoice in one call, finds it by id, number and status, records a payment
/// taken elsewhere, refuses what the invoice's state refuses, answers forbidden without the permission, and shows a
/// customer only their own issued invoices, without staff's note.
/// </summary>
public sealed class InvoicingApiTests
{
    [Fact]
    public async Task AnInvoice_IsDraftedAndIssued_ThenFound_ThenPaidInPart()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();

        // Act
        var created = await CallAsync(harness, (services, context) => InvoicingApiEndpoints.CreateAsync(
            new ApiInvoiceDraft
            {
                BillTo = new ApiInvoiceDraftParty { Party = "User:user-1", Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" },
                Currency = "usd",
                Reference = "Deal 42",
                InternalNote = "Agreed by phone",
                Lines = [new ApiInvoiceDraftLine { Name = "Consulting", Quantity = 2m, UnitPrice = 50m }],
                Issue = true,
            },
            context,
            Allow(),
            services.GetRequiredService<IInvoiceManager>(),
            services.GetRequiredService<IInvoiceCommandService>(),
            services.GetRequiredService<IDocumentCalculator>(),
            services.GetRequiredService<ISiteService>(),
            services.GetRequiredService<global::YesSql.ISession>()));

        var invoice = Assert.IsType<Created<ApiInvoice>>(created.Result).Value;
        var listed = await CallAsync(harness, (services, context) => InvoicingApiEndpoints.ListAsync("open", null, "User:user-1", null, null, null, context, Allow(), services.GetRequiredService<IInvoiceManager>()));
        var byNumber = await CallAsync(harness, (services, context) => InvoicingApiEndpoints.GetByNumberAsync(invoice.Number, context, Allow(), services.GetRequiredService<IInvoiceStore>()));
        var unknown = await CallAsync(harness, (services, context) => InvoicingApiEndpoints.GetAsync("nope", context, Allow(), services.GetRequiredService<IInvoiceManager>()));
        var paid = await CallAsync(harness, (services, context) => InvoicingApiEndpoints.RecordPaymentAsync(invoice.Id, new ApiInvoicePaymentRequest { Amount = 40m, Method = "check", Reference = "CHK-1" }, context, Allow(), services.GetRequiredService<IInvoiceManager>(), services.GetRequiredService<ITransactionCommandService>(), services.GetRequiredService<InvoiceLedgerSync>()));
        var voided = await CallAsync(harness, (services, context) => InvoicingApiEndpoints.VoidAsync(invoice.Id, new ApiInvoiceReasonRequest { Reason = "Wrong" }, context, Allow(), services.GetRequiredService<IInvoiceCommandService>()));

        // Assert
        Assert.Equal(("open", "unpaid", "USD", 100m), (invoice.Status, invoice.Settlement, invoice.Currency, invoice.Total.Amount));
        Assert.False(string.IsNullOrEmpty(invoice.Number));
        Assert.Equal([("Consulting", 2m, 50m)], invoice.Lines.Select(line => (line.Name, line.Quantity, line.UnitPrice)));

        var page = Assert.IsType<Ok<ApiPage<ApiInvoiceSummary>>>(listed.Result).Value;
        Assert.Equal([invoice.Id], page.Items.Select(item => item.Id));
        Assert.Equal(invoice.Id, Assert.IsType<Ok<ApiInvoice>>(byNumber.Result).Value.Id);
        Assert.IsType<ProblemHttpResult>(unknown.Result);

        var afterPayment = Assert.IsType<Ok<ApiInvoice>>(paid.Result).Value;
        Assert.Equal(("partiallyPaid", 40m, 60m), (afterPayment.Settlement, afterPayment.AmountPaid.Amount, afterPayment.AmountDue.Amount));
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(voided.Result).StatusCode);
    }

    [Fact]
    public async Task WithoutThePermission_TheAnswerIsForbidden()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();

        // Act
        var listed = await CallAsync(harness, (services, context) => InvoicingApiEndpoints.ListAsync(null, null, null, null, null, null, context, Deny(), services.GetRequiredService<IInvoiceManager>()));

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ProblemHttpResult>(listed.Result).StatusCode);
    }

    [Fact]
    public async Task ACustomer_SeesOnlyTheirOwnIssuedInvoices_WithoutStaffsNote()
    {
        // Arrange: one invoice issued to user-1, one to user-2, and a draft for user-1.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var mine = await IssueAsync(harness, "user-1");
        var theirs = await IssueAsync(harness, "user-2");
        var draft = await harness.CreateDraftAsync(invoice => invoice.BillTo = new DocumentParty { Party = new PartyRef(PartyKinds.User, "user-1") });

        // Act
        var own = await CallAsync(harness, (services, context) => InvoicingApiEndpoints.GetMineAsync(mine.ItemId, context, Allow(), services.GetRequiredService<IInvoiceVisibilityService>(), services.GetRequiredService<IInvoiceManager>()));
        var other = await CallAsync(harness, (services, context) => InvoicingApiEndpoints.GetMineAsync(theirs.ItemId, context, Allow(), services.GetRequiredService<IInvoiceVisibilityService>(), services.GetRequiredService<IInvoiceManager>()));
        var unissued = await CallAsync(harness, (services, context) => InvoicingApiEndpoints.GetMineAsync(draft.ItemId, context, Allow(), services.GetRequiredService<IInvoiceVisibilityService>(), services.GetRequiredService<IInvoiceManager>()));

        // Assert
        var invoice = Assert.IsType<Ok<ApiInvoice>>(own.Result).Value;
        Assert.Null(invoice.InternalNote);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(other.Result).StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(unissued.Result).StatusCode);
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness, string userId)
    {
        var draft = await harness.CreateDraftAsync(invoice =>
        {
            invoice.BillTo = new DocumentParty { Party = new PartyRef(PartyKinds.User, userId), Contact = new ContactInfo { Name = userId, Email = $"{userId}@example.com" } };
            invoice.InternalNote = "Staff only";
        });

        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }

    private static Task<T> CallAsync<T>(InvoicingTestHarness harness, Func<IServiceProvider, HttpContext, Task<T>> call)
        => harness.InScopeAsync(services => call(services, new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test")),
        }));

    private static IAuthorizationService Allow()
        => Authorization(true);

    private static IAuthorizationService Deny()
        => Authorization(false);

    private static IAuthorizationService Authorization(bool allowed)
    {
        var service = new Mock<IAuthorizationService>();
        service
            .Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        return service.Object;
    }
}
