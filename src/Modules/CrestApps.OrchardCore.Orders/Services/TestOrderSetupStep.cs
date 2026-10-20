using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The setup guide's last step: buy something from the store as a customer would, to see the whole sale work.
/// </summary>
internal sealed class TestOrderSetupStep : IStoreSetupStep
{
    private readonly IOrderStore _orders;
    private readonly AdminOptions _adminOptions;

    internal readonly IStringLocalizer S;

    public TestOrderSetupStep(IOrderStore orders, IOptions<AdminOptions> adminOptions, IStringLocalizer<TestOrderSetupStep> stringLocalizer)
    {
        _orders = orders;
        _adminOptions = adminOptions.Value;
        S = stringLocalizer;
    }

    public string Name
        => "test-order";

    public int Order
        => 80;

    public async Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var (count, orders) = await _orders.PageAsync(new OrderQuery { Statuses = [OrderStatus.Paid] }, 0, 1, cancellationToken);

        return new StoreSetupStepDescription
        {
            Title = S["Place a test order"],
            Description = S["Open your store, add a product to the cart and check out as a customer would. With Stripe in test mode, pay with the card 4242 4242 4242 4242, any future date and any three digits: no money is taken. Then find the order under Commerce, Orders, and refund or cancel it."],
            IsDone = count > 0,
            Status = count == 0 ? null : S["Order {0} was paid.", orders[0].OrderNumber],
            Url = count == 0 ? "~/" : $"~/{_adminOptions.AdminUrlPrefix}/orders",
            UrlText = count == 0 ? S["Open your store"] : S["Orders"],
        };
    }
}
