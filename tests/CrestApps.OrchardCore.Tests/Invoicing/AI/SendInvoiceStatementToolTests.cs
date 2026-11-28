using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>sendInvoiceStatement</c> e-mails a customer's statement to the address the statement service finds for them, never
/// one the model gives, and needs Manage invoices, stricter than the Statements page.
/// </summary>
public sealed class SendInvoiceStatementToolTests
{
    private static readonly PartyRef _customer = new(PartyKinds.User, "user-7");

    private readonly InvoicingLifecycleToolContext _context = new();

    [Fact]
    public async Task SendInvoiceStatement_NeedsManageInvoices_AndASignedInCaller()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var statements = new Mock<IInvoiceStatementService>();
        var arguments = new Dictionary<string, object> { ["customer"] = _customer.ToString() };

        // Act
        var viewOnly = await _context.InvokeAsync(harness, new SendInvoiceStatementTool(), arguments, allow: InvoicingLifecycleToolContext.Only(InvoicingPermissions.ViewInvoices), services: Services(statements));
        var anonymous = await _context.InvokeAsync(harness, new SendInvoiceStatementTool(), arguments, signedIn: false, services: Services(statements));

        // Assert
        Assert.Contains("not allowed", viewOnly, StringComparison.Ordinal);
        Assert.Contains("not allowed", anonymous, StringComparison.Ordinal);
        statements.Verify(service => service.SendAsync(It.IsAny<InvoiceStatement>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendInvoiceStatement_SendsTheOpenItemStatementOfToday_ToTheCustomersOwnAddress()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var statements = new Mock<IInvoiceStatementService>();
        var statement = new InvoiceStatement
        {
            Currency = "USD",
            ClosingBalance = 100m,
            Recipient = new DocumentParty { Party = _customer, Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } },
        };
        InvoiceStatementRequest asked = null;
        statements
            .Setup(service => service.BuildAsync(It.IsAny<InvoiceStatementRequest>(), It.IsAny<CancellationToken>()))
            .Callback((InvoiceStatementRequest request, CancellationToken _) => asked = request)
            .ReturnsAsync(statement);
        statements
            .Setup(service => service.SendAsync(statement, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null);

        // Act
        var answer = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new SendInvoiceStatementTool(), new() { ["customer"] = _customer.ToString() }, services: Services(statements)));

        // Assert
        Assert.Equal((_customer, InvoiceStatementKind.OpenItem, DateOnly.FromDateTime(InvoicingTestHarness.Now), (DateOnly?)null), (asked.Party, asked.Kind, asked.To, asked.From));
        statements.Verify(service => service.SendAsync(statement, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(("ap@fabrikam.example", 100m), (answer.GetProperty("sentTo").GetString(), answer.GetProperty("closingBalance").GetDecimal()));
    }

    [Fact]
    public async Task SendInvoiceStatement_TakesNoAddress_AndRelaysWhyItWasNotSent()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var statements = new Mock<IInvoiceStatementService>();
        statements.Setup(service => service.BuildAsync(It.IsAny<InvoiceStatementRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new InvoiceStatement());
        statements.Setup(service => service.SendAsync(It.IsAny<InvoiceStatement>(), null, It.IsAny<CancellationToken>())).ReturnsAsync("The customer has no e-mail address to send the statement to.");

        // Act
        var withAddress = await _context.InvokeAsync(harness, new SendInvoiceStatementTool(), new() { ["customer"] = _customer.ToString(), ["sendTo"] = "attacker@example.com" }, services: Services(statements));
        var notSent = await _context.InvokeAsync(harness, new SendInvoiceStatementTool(), new() { ["customer"] = _customer.ToString() }, services: Services(statements));

        // Assert
        Assert.Contains("'sendTo' is not an argument", withAddress, StringComparison.Ordinal);
        Assert.Contains("no e-mail address", notSent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendInvoiceStatement_StartsAnActivityStatementOnTheFirstOfTheMonth()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var statements = new Mock<IInvoiceStatementService>();
        InvoiceStatementRequest asked = null;
        statements
            .Setup(service => service.BuildAsync(It.IsAny<InvoiceStatementRequest>(), It.IsAny<CancellationToken>()))
            .Callback((InvoiceStatementRequest request, CancellationToken _) => asked = request)
            .ReturnsAsync(new InvoiceStatement { Currency = "USD" });
        statements.Setup(service => service.SendAsync(It.IsAny<InvoiceStatement>(), null, It.IsAny<CancellationToken>())).ReturnsAsync((string)null);

        // Act
        await _context.InvokeAsync(harness, new SendInvoiceStatementTool(), new() { ["customer"] = _customer.ToString(), ["kind"] = "activity", ["to"] = "2026-10-31" }, services: Services(statements));

        // Assert
        Assert.Equal((InvoiceStatementKind.Activity, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), (asked.Kind, asked.From.Value, asked.To));
    }

    private static Dictionary<Type, object> Services(Mock<IInvoiceStatementService> statements)
        => new() { [typeof(IInvoiceStatementService)] = statements.Object };
}
