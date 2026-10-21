using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// A store's templates of its order emails read the order as plain values, and an email without a template goes as built.
/// </summary>
public sealed class OrderEmailTemplateTests
{
    [Fact]
    public async Task AnEmail_ReachesTheTemplatesAsBuilt()
    {
        var templates = new Mock<IOrderEmailTemplates>();
        templates.Setup(value => value.ApplyAsync(It.IsAny<OrderEmailContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderEmailContext context, CancellationToken _) => new OrderEmailMessage(context.Subject, context.Text, context.Html));

        var email = await templates.Object.ApplyAsync(OrderEmailKinds.Confirmation, new Order(), "Subject", "Text", "<p>Html</p>", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new OrderEmailMessage("Subject", "Text", "<p>Html</p>"), email);
    }

    [Fact]
    public void ATemplate_ReadsTheOrderAsPlainValues_WithStatusesByName()
    {
        var order = new Order
        {
            OrderNumber = "261020-TEST",
            Status = OrderStatus.Paid,
            Subtotal = 79m,
            Lines = [new OrderLine { Title = "Backpack", Quantity = 2 }],
        };

        var plain = Assert.IsType<Dictionary<string, object>>(LiquidOrderEmailTemplates.ToPlain(LiquidOrderEmailTemplates.ToJson(order)));

        Assert.Equal("261020-TEST", plain["ordernumber"]);
        Assert.Equal("Paid", plain["Status"]);
        Assert.Equal(79m, plain["Subtotal"]);

        var line = Assert.IsType<Dictionary<string, object>>(Assert.Single(Assert.IsType<List<object>>(plain["Lines"])));

        Assert.Equal("Backpack", line["Title"]);
        Assert.Equal(2m, line["Quantity"]);
    }

    [Fact]
    public void AnEmailsText_FollowsItsHtml()
        => Assert.Equal("We got it!\n1 × Backpack", LiquidOrderEmailTemplates.ToText("<p>We got it!</p><div>1 &#xD7; Backpack</div>"));
}
