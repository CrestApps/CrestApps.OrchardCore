using System.Globalization;
using System.Reflection;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// F4 (third fix verification): since a sign-up starts with a post, the amount a buyer names comes from a form, and MVC
/// binds a form value in the request's culture, while a number input and the confirmation page's hidden field always
/// write a dot. In de-DE <c>12.50</c> bound as 1250, and the checkout started for a hundred times the amount; in fr-FR it
/// did not bind, and the checkout started at the price's default amount. These tests post through MVC's own value
/// providers and model binders, under the request culture, as the action is called in a running site.
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

    // Binds every parameter of SignupPost the way ControllerBinderDelegateProvider does: the form, query and route value
    // providers MVC registers, and the binder MVC's factory picks for each parameter. The form value provider reads the
    // culture when it is created, so the request's culture is set first, as the request localization middleware does.
    private static async Task<(IActionResult Result, StartCheckoutRequest Request)> PostAsync(string culture, string amount)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;

        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

        try
        {
            using var mvc = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();

            StartCheckoutRequest started = null;
            var controller = SubscriptionSignupLinkTests.CreateController(request => started = request);
            var method = typeof(SubscriptionsController).GetMethod(nameof(SubscriptionsController.SignupPost));

            var httpContext = new DefaultHttpContext { RequestServices = mvc };
            httpContext.Request.Method = HttpMethods.Post;
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";

            var form = new Dictionary<string, StringValues> { ["quantity"] = "1" };

            if (amount is not null)
            {
                form["customAmount"] = amount;
            }

            httpContext.Request.Form = new FormCollection(form);

            var routeData = new RouteData();
            routeData.Values["contentItemId"] = "plan-1";

            var controllerContext = new ControllerContext(new ActionContext(httpContext, routeData, new ControllerActionDescriptor
            {
                MethodInfo = method,
                ControllerTypeInfo = typeof(SubscriptionsController).GetTypeInfo(),
            }))
            {
                ValueProviderFactories = [.. mvc.GetRequiredService<IOptions<MvcOptions>>().Value.ValueProviderFactories],
            };

            controller.ControllerContext = controllerContext;

            var arguments = await BindAsync(mvc, controllerContext, method);
            var result = await (Task<IActionResult>)method.Invoke(controller, arguments);

            return (result, started);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    private static async Task<object[]> BindAsync(IServiceProvider mvc, ControllerContext controllerContext, MethodInfo method)
    {
        var metadataProvider = mvc.GetRequiredService<IModelMetadataProvider>();
        var binderFactory = mvc.GetRequiredService<IModelBinderFactory>();
        var parameterBinder = mvc.GetRequiredService<ParameterBinder>();
        var valueProvider = await CompositeValueProvider.CreateAsync(controllerContext);

        var parameters = method.GetParameters();
        var arguments = new object[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var metadata = ((ModelMetadataProvider)metadataProvider).GetMetadataForParameter(parameter);
            var bindingInfo = BindingInfo.GetBindingInfo(parameter.GetCustomAttributes(), metadata);
            var binder = binderFactory.CreateBinder(new ModelBinderFactoryContext
            {
                Metadata = metadata,
                BindingInfo = bindingInfo,
                CacheToken = parameter,
            });

            var descriptor = new ControllerParameterDescriptor
            {
                Name = parameter.Name,
                ParameterType = parameter.ParameterType,
                BindingInfo = bindingInfo,
                ParameterInfo = parameter,
            };

            var bound = await parameterBinder.BindModelAsync(controllerContext, binder, valueProvider, descriptor, metadata, value: null, container: null);

            arguments[i] = bound.IsModelSet ? bound.Model : parameter.HasDefaultValue ? parameter.DefaultValue : null;
        }

        return arguments;
    }
}
