using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Tests.Core.ModelBinding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// F4 (third fix verification): since a sign-up starts with a post, the amount a buyer names comes from a form, and MVC
/// binds a form value in the request's culture, while a number input and the confirmation page's hidden field always
/// write a dot. In de-DE <c>12.50</c> bound as 1250, and the checkout started for a hundred times the amount; in fr-FR it
/// did not bind, and the checkout started at the price's default amount. These tests post through MVC's own value
/// providers and model binders, with the module's invariant number binding, under the request culture, as the action is
/// called in a running site.
/// </summary>
public sealed class SubscriptionSignupAmountBindingTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("nl-NL")]
    public async Task SignupPost_TheAmountANumberInputSubmits_IsReadTheSameInEveryCulture(string culture)
    {
        var (result, request) = await PostAsync(culture, "12.50");

        Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal(12.50m, request?.PriceSelection.CustomAmount);
    }

    // A theme that writes the amount in a text box lets the buyer type it the way the store's culture writes it.
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task SignupPost_AnAmountWrittenWithTheCulturesDecimalComma_IsReadAsThatAmount(string culture)
    {
        var (_, request) = await PostAsync(culture, "12,50");

        Assert.Equal(12.50m, request?.PriceSelection.CustomAmount);
    }

    // A group separator is never taken: "1,250" is not twelve hundred and fifty in one culture and 1.25 in another.
    [Theory]
    [InlineData("en-US", "12,50")]
    [InlineData("en-US", "1,250")]
    [InlineData("de-DE", "1.250,00")]
    [InlineData("de-DE", "twelve")]
    public async Task SignupPost_AnAmountThatIsNotANumber_StartsNoCheckout(string culture, string amount)
    {
        var (result, request) = await PostAsync(culture, amount);

        Assert.Null(request);
        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task SignupPost_WithoutAnAmount_StartsTheCheckoutAtThePricesAmount()
    {
        var (result, request) = await PostAsync("de-DE", amount: null);

        Assert.IsType<RedirectToRouteResult>(result);
        Assert.Null(request?.PriceSelection.CustomAmount);
    }

    // Binds every parameter of SignupPost with MVC's own value providers and model binders, and the services the
    // Subscriptions module's Startup registers, under the request's culture, then calls the action.
    private static async Task<(IActionResult Result, StartCheckoutRequest Request)> PostAsync(string culture, string amount)
    {
        StartCheckoutRequest started = null;
        var controller = SubscriptionSignupLinkTests.CreateController(request => started = request);
        var method = typeof(SubscriptionsController).GetMethod(nameof(SubscriptionsController.SignupPost));
        var form = new Dictionary<string, StringValues> { ["quantity"] = "1" };

        if (amount is not null)
        {
            form["customAmount"] = amount;
        }

        var bound = await FormPostBinder.BindAsync(
            new CrestApps.OrchardCore.Subscriptions.Startup(),
            method,
            culture,
            form,
            new Dictionary<string, object> { ["contentItemId"] = "plan-1" },
            controller);

        var result = await (Task<IActionResult>)method.Invoke(controller, bound.Arguments);

        return (result, started);
    }
}
